using ConstantLearning.Features.Content.References;
using ConstantLearning.Tests.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NodaTime;

namespace ConstantLearning.Tests.Content.References;

public sealed class ReferenceSnapshotIntegrationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private ReferenceSnapshotImporter Importer() => new(Db, new(new()), new(), Clock);
    private WikislownikReferenceSource Source() => new(Db, Clock);

    [DockerFact]
    public async Task ConfiguredLocalDump_UsesOperatorTimestamp_AndBecomesAvailable()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            using (var dump = ReferenceFixtures.Dump(("fixture-configured", ReferenceFixtures.Read("supported"), "92345")))
            await using (var file = File.Create(path)) await dump.CopyToAsync(file);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["References:WikislownikDumpPath"] = path,
                ["References:SnapshotAt"] = "2026-10-31T08:00:00Z"
            }).Build();
            var imported = await Importer().ImportConfiguredDumpAsync(configuration);
            imported.Imported.Should().Be(1);
            var result = await Source().LookupAsync(new("fixture-configured", "translation"));
            result.Status.Should().Be(ReferenceStatus.Available);
            result.Candidates.Should().ContainSingle().Which.SnapshotAt.Should().Be(Instant.FromUtc(2026, 10, 31, 8, 0));
        }
        finally { File.Delete(path); }
    }

    [DockerFact]
    public async Task ConfiguredDump_WithoutMetadataOrFile_ReportsManualReason()
    {
        var missingTimestamp = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["References:WikislownikDumpPath"] = "missing.xml"
        }).Build();
        (await Importer().ImportConfiguredDumpAsync(missingTimestamp)).Issues.Should().ContainSingle()
            .Which.Reason.Should().Be("configured_snapshot_metadata_invalid");
        var missingFile = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["References:WikislownikDumpPath"] = "missing.xml", ["References:SnapshotAt"] = "2026-10-31T08:00:00Z"
        }).Build();
        (await Importer().ImportConfiguredDumpAsync(missingFile)).Issues.Should().ContainSingle()
            .Which.Reason.Should().Be("configured_dump_missing");
    }

    [DockerFact]
    public async Task Dump_StoresRevisionAndSnapshot_ReimportsIdempotently_AndLooksUpThroughPort()
    {
        var snapshot = Clock.GetCurrentInstant();
        using var first = ReferenceFixtures.Dump(("fixture-kot", ReferenceFixtures.Read("supported"), "12345"));
        (await Importer().ImportWikislownikDumpAsync(first, snapshot)).Imported.Should().Be(1);
        using var repeated = ReferenceFixtures.Dump(("fixture-kot", ReferenceFixtures.Read("supported"), "12345"));
        (await Importer().ImportWikislownikDumpAsync(repeated, snapshot)).Unchanged.Should().Be(1);

        IReferenceSource source = Source();
        var result = await source.LookupAsync(new("fixture-kot", "translation"));
        result.Status.Should().Be(ReferenceStatus.Available);
        result.Candidates.Should().ContainSingle().Which.Should().Be(new ReferenceCandidate("кіт", "wikislownik",
            "https://pl.wiktionary.org/wiki/fixture-kot", "12345", snapshot, WikislownikReferenceSource.Attribution, "CC-BY-SA-4.0"));
        (await source.LookupAsync(new("fixture-kot", "transcription"))).Candidates.Should().ContainSingle().Which.Value.Should().Be("кот");
        (await source.LookupAsync(new("fixture-kot", "example"))).Reason.Should().Be("unsupported_field");
    }

    [DockerFact]
    public async Task ParserFailure_ReplacesPreviousUsableSnapshot_AndBatchContinues()
    {
        var snapshot = Clock.GetCurrentInstant();
        using var initial = ReferenceFixtures.Dump(("fixture-broken", ReferenceFixtures.Read("supported"), "1"));
        await Importer().ImportWikislownikDumpAsync(initial, snapshot - Duration.FromDays(1));
        using var changed = ReferenceFixtures.Dump(
            ("fixture-broken", ReferenceFixtures.Read("unsupported"), "2"),
            ("fixture-good", ReferenceFixtures.Read("supported"), "3"));
        var imported = await Importer().ImportWikislownikDumpAsync(changed, snapshot);
        imported.Imported.Should().Be(2);
        imported.ManualRecords.Should().Be(1);
        (await Source().LookupAsync(new("fixture-broken", "translation"))).Status.Should().Be(ReferenceStatus.ManualReview);
        (await Source().LookupAsync(new("fixture-good", "translation"))).Status.Should().Be(ReferenceStatus.Available);
        (await Db.ReferenceEntries.Where(entry => entry.Lemma == "fixture-broken").ToListAsync())
            .Should().OnlyContain(entry => entry.RevisionId == "2");
    }

    [DockerFact]
    public async Task StaleSnapshotAndMissingRevision_RequireEditor()
    {
        using var dump = ReferenceFixtures.Dump(("fixture-stale", ReferenceFixtures.Read("supported"), "4"),
            ("fixture-norev", ReferenceFixtures.Read("supported"), ""));
        await Importer().ImportWikislownikDumpAsync(dump, Clock.GetCurrentInstant() - Duration.FromDays(36));
        (await Source().LookupAsync(new("fixture-stale", "translation"))).Reason.Should().Be("snapshot_stale");
        (await Source().LookupAsync(new("fixture-norev", "translation"))).Status.Should().Be(ReferenceStatus.ManualReview);
    }

    [DockerFact]
    public async Task FoldedCollision_DoesNotConfirmAnotherLemma_AndOldSnapshotCannotReplaceNew()
    {
        var now = Clock.GetCurrentInstant();
        using var current = ReferenceFixtures.Dump(("fixture-żółć", ReferenceFixtures.Read("supported"), "8"));
        await Importer().ImportWikislownikDumpAsync(current, now);
        (await Source().LookupAsync(new("fixture-zolc", "translation"))).Reason.Should().Be("reference_missing");
        using var older = ReferenceFixtures.Dump(("fixture-żółć", ReferenceFixtures.Read("unsupported"), "7"));
        (await Importer().ImportWikislownikDumpAsync(older, now - Duration.FromDays(1))).Unchanged.Should().Be(1);
        var found = await Source().LookupAsync(new("fixture-żółć", "translation"));
        found.Status.Should().Be(ReferenceStatus.Available);
        found.Candidates.Should().OnlyContain(candidate => candidate.RevisionId == "8");
    }

    [DockerFact]
    public async Task RealEvidence_ImportsLocalTwoThousandWithoutInventingRevisions()
    {
        var directory = Path.Combine(ReferenceFixtures.Root, "docs", "dictionary");
        var first = await Importer().ImportEvidenceFileAsync(Path.Combine(directory, "evidence.jsonl"), Path.Combine(directory, "sources-manifest.json"));
        (first.Imported + first.Unchanged).Should().Be(2000);
        first.ManualRecords.Should().Be(2000);
        first.Issues.Should().BeEmpty();
        var lookup = await Source().LookupAsync(new("dom", "translation"));
        lookup.Status.Should().Be(ReferenceStatus.ManualReview);
        lookup.Reason.Should().Be("revision_missing");
        lookup.Candidates.Should().NotBeEmpty().And.OnlyContain(candidate => candidate.RevisionId == null);
    }

    [DockerFact]
    public async Task MalformedEvidence_DoesNotStopFollowingRecord()
    {
        using var input = new StringReader("{bad\n" +
            "{\"lemma_pl\":\"fixture-evidence\",\"source_url\":\"https://pl.wiktionary.org/wiki/fixture-evidence\",\"ipa\":\"kɔt\",\"translation_candidates\":[\"кіт\"]}\n");
        var result = await Importer().ImportEvidenceAsync(input, Clock.GetCurrentInstant());
        result.Imported.Should().Be(1);
        result.Issues.Should().ContainSingle().Which.Reason.Should().Be("invalid_evidence_record");
    }
}
