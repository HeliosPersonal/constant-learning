using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Import;
using FluentAssertions;

namespace ConstantLearning.Tests.Content.Import;

public sealed class DictionaryImportReaderTests
{
    private readonly DictionaryImportReader _reader = new();

    [Fact]
    public async Task PackagedRelease_ValidatesAllTwoThousandRowsAndEvidence()
    {
        var dataset = await _reader.ReadAsync(Path.Combine(AppContext.BaseDirectory, "Resources", "Dictionary"));
        var rows = dataset.Rows;

        dataset.Language.Should().Be("pl");
        rows.Should().HaveCount(2000);
        rows.Should().OnlyContain(row => row["validation_status"] == "approved" && row.Evidence.HasValue);
        rows.Select(row => row["id"]).Should().OnlyHaveUniqueItems();
        rows.Should().Contain(row => row["gender"] == "pl");
        rows.Should().Contain(row => row.Evidence!.Value.GetProperty("translation_correction").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Csv_HandlesQuotesCommasMultilineAndDeterministicHash()
    {
        using var bundle = new DictionaryImportTestBundle();
        bundle.Row["example_pl"] = "To jest żaba,\nktóra mówi: \"kum\".";
        await bundle.WriteAsync();
        var first = (await _reader.ReadAsync(bundle.DirectoryPath)).Rows.Single();
        first["example_pl"].Should().Be(bundle.Row["example_pl"]);
        first.Values("translation_uk").Should().Equal("жаба", "жабка");

        await bundle.WriteAsync(quoteAll: true, reverseEvidence: true);
        var second = (await _reader.ReadAsync(bundle.DirectoryPath)).Rows.Single();
        second.ContentHash.Should().Be(first.ContentHash);

        bundle.Row["example_uk"] = "Це маленька жаба.";
        await bundle.WriteAsync();
        (await _reader.ReadAsync(bundle.DirectoryPath)).Rows.Single().ContentHash.Should().NotBe(first.ContentHash);
    }

    [Theory]
    [InlineData("gender", "")]
    [InlineData("validation_sources", "missing")]
    [InlineData("translation_uk", "непідтверджений переклад")]
    [InlineData("accepted_answers_pl", "żaba;ropucha")]
    [InlineData("pronunciation_uk", "zaba")]
    [InlineData("reviewed_by", "human")]
    [InlineData("frequency_rank", "-2")]
    public async Task Approved_RejectsInvalidContent(string field, string value)
    {
        using var bundle = new DictionaryImportTestBundle();
        bundle.Row[field] = value;
        await bundle.WriteAsync();
        await FluentActions.Awaiting(() => _reader.ReadAsync(bundle.DirectoryPath)).Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Approved_AcceptsPluralAndExplicitlySourcedAnswerVariants()
    {
        using var bundle = new DictionaryImportTestBundle();
        bundle.Row["gender"] = "pl";
        bundle.Row["accepted_answers_pl"] = "żaba;żaby";
        bundle.Evidence["source_tags"] = new JsonArray("plural");
        bundle.Evidence["accepted_answers_pl"] = new JsonArray("żaby");
        await bundle.WriteAsync();
        var row = (await _reader.ReadAsync(bundle.DirectoryPath)).Rows.Single();
        row["gender"].Should().Be("pl");
        row.Values("accepted_answers_pl").Should().Equal("żaba", "żaby");
    }

    [Fact]
    public async Task Release_RejectsChecksumMismatchAndMissingManifest()
    {
        using var bundle = new DictionaryImportTestBundle();
        await bundle.WriteAsync();
        await File.AppendAllTextAsync(Path.Combine(bundle.DirectoryPath, "course.csv"), "tampered");
        await FluentActions.Awaiting(() => _reader.ReadAsync(bundle.DirectoryPath)).Should().ThrowAsync<InvalidDataException>();
        File.Delete(Path.Combine(bundle.DirectoryPath, "readiness.json"));
        await FluentActions.Awaiting(() => _reader.ReadAsync(bundle.DirectoryPath)).Should().ThrowAsync<FileNotFoundException>();
    }
}
