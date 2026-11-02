using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit.Abstractions;

namespace ConstantLearning.Tests.Content.Search.Api;

public sealed class DictionarySearchBenchmarkTests(PostgresFixture postgres, ITestOutputHelper output)
    : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Search_500RequestsOfOneToFiveCharacters_With2000Entries_ServerP95Within300Milliseconds()
    {
        const long userId = 900000099;
        Db.Learners.Add(new Learner { TelegramUserId = userId, FirstName = "Вимірювання", TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = true, DailyNewWordsLimit = 5 });
        var entries = new List<DictionaryEntry>();
        for (var index = 0; index < 2000; index++)
        {
            var entry = new DictionaryEntry { ExternalId = $"benchmark-{index}", Domain = "everyday", CoursePosition = index + 1 };
            var version = ReportsPublicationTestData.NewVersion(entry);
            version.LemmaPl = index == 0 ? "a" : $"żaba{index:D4}";
            entries.Add(entry);
            Db.EntryVersions.Add(version);
            Db.VersionTranslations.Add(new() { VersionId = version.Id, Kind = "canonical", TextUk = $"жаба{index:D4}", Position = 0 });
            foreach (var form in new[] { version.LemmaPl, $"żabami{index:D4}", $"żabie{index:D4}", $"żabą{index:D4}",
                         $"żaby{index:D4}", $"żabom{index:D4}", $"żabach{index:D4}", $"żab{index:D4}",
                         $"żabce{index:D4}", $"żabkę{index:D4}", $"żabko{index:D4}", $"żabki{index:D4}" })
                Db.SearchForms.Add(new SearchForm { EntryId = entry.Id, VersionId = version.Id, Lang = "pl", FormDisplay = form,
                    FormFolded = SearchTextNormalizer.Fold(form), IsLemma = form == version.LemmaPl });
            Db.SearchForms.Add(new SearchForm { EntryId = entry.Id, VersionId = version.Id, Lang = "uk",
                FormDisplay = $"жаба{index:D4}", FormFolded = $"жаба{index:D4}" });
        }
        await Db.SaveChangesAsync();
        foreach (var entry in entries) entry.PublishedVersionId = Db.EntryVersions.Local.Single(version => version.EntryId == entry.Id).Id;
        await Db.SaveChangesAsync();
        await Db.Database.ExecuteSqlRawAsync("ANALYZE dictionary_entries; ANALYZE entry_versions; ANALYZE search_forms; ANALYZE version_translations;");
        await using var app = await DictionaryTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(userId, Clock.GetCurrentInstant()));
        string[] queries = ["a", "za", "zab", "zaba", "zabam"];
        foreach (var query in Enumerable.Range(0, 25).Select(index => queries[index % queries.Length]))
        {
            using var warmup = await app.Client.GetAsync("/api/v1/dictionary/search?q=" + query);
            Assert.Equal(HttpStatusCode.OK, warmup.StatusCode);
            await warmup.Content.ReadAsStringAsync();
        }
        app.ServerMilliseconds.Clear();
        var clientTimes = new List<double>();
        var samples = new List<(int Length, double Server, double Client)>();
        for (var index = 0; index < 500; index++)
        {
            var started = Stopwatch.GetTimestamp();
            using var response = await app.Client.GetAsync("/api/v1/dictionary/search?q=" + queries[index % queries.Length]);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await response.Content.ReadAsStringAsync();
            clientTimes.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        var serverTimes = app.ServerMilliseconds.ToArray();
        Assert.Equal(500, serverTimes.Length);
        for (var index = 0; index < serverTimes.Length; index++) samples.Add((index % queries.Length + 1, serverTimes[index], clientTimes[index]));
        var p95 = Percentile(serverTimes, 0.95);
        var reportPath = await WriteReportAsync(samples);
        output.WriteLine($"p95 сервера: {p95:F3} ms; звіт: {reportPath}");
        Assert.True(p95 <= 300, $"p95 сервера {p95:F3} ms перевищує 300 ms. Звіт: {reportPath}");
    }

    private async Task<string> WriteReportAsync(List<(int Length, double Server, double Client)> samples)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ConstantLearning.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root.FullName, "docs", "features", "personal-trainer-mvp", "test-output");
        Directory.CreateDirectory(directory);
        await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT version()", connection);
        var databaseVersion = (string)(await command.ExecuteScalarAsync())!;
        var report = new StringBuilder();
        report.AppendLine("# Вимірювання затримки пошуку у словнику");
        report.AppendLine();
        report.AppendLine("Фактичний прогін для [T22](../tasks/T22-dictionary-search-api.md), [AC-30](../PRD.md) та [ADR-0012](../adr/0012-search-via-a-precomputed-table-of-folded-word-forms.md).");
        report.AppendLine();
        report.AppendLine($"- Час UTC: {DateTimeOffset.UtcNow:O}.");
        report.AppendLine($"- ОС процесу: {RuntimeInformation.OSDescription}; архітектура: {RuntimeInformation.ProcessArchitecture}.");
        report.AppendLine($"- CPU зі змінної середовища PROCESSOR_IDENTIFIER: {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "не надано середовищем"}.");
        report.AppendLine($"- Доступні логічні CPU: {Environment.ProcessorCount}; пам’ять, доступна .NET GC: {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024} MiB.");
        report.AppendLine($"- Runtime: {RuntimeInformation.FrameworkDescription}; конфігурація: Release.");
        report.AppendLine($"- PostgreSQL у локальному Docker: {databaseVersion}.");
        report.AppendLine("- HTTP через ASP.NET Core TestServer; справжня перевірка Telegram HMAC та запити EF Core до PostgreSQL; мережеву затримку зовнішнього клієнта не вимірює.");
        report.AppendLine("- Фікстура: 2 000 опублікованих approved-записів, 26 000 форм, 2 000 перекладів. ANALYZE після заповнення.");
        report.AppendLine("- 25 прогрівальних запитів не входять у вибірку. 500 послідовних запитів, паралельність 1, по 100 кожної довжини: `a`, `za`, `zab`, `zaba`, `zabam`.");
        report.AppendLine("- Серверний час: middleware від початку обробки HTTP до повернення pipeline, включно з автентифікацією, запитами БД і серіалізацією. Перцентиль: nearest rank.");
        report.AppendLine();
        report.AppendLine("| Довжина | Запити | p50 сервера, ms | p95 сервера, ms | Максимум сервера, ms | p95 клієнта, ms |");
        report.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var length in Enumerable.Range(1, 5).Append(0))
        {
            var rows = samples.Where(sample => length == 0 || sample.Length == length).ToArray();
            report.AppendLine(FormattableString.Invariant($"| {(length == 0 ? "Усі" : length.ToString(CultureInfo.InvariantCulture))} | {rows.Length} | {Percentile(rows.Select(row => row.Server), 0.5):F3} | {Percentile(rows.Select(row => row.Server), 0.95):F3} | {rows.Max(row => row.Server):F3} | {Percentile(rows.Select(row => row.Client), 0.95):F3} |"));
        }
        report.AppendLine();
        report.AppendLine($"Умова p95 сервера ≤ 300 ms: {(Percentile(samples.Select(sample => sample.Server), 0.95) <= 300 ? "виконана" : "НЕ виконана")}. [Сирі вимірювання](dictionary-search-samples.csv).");
        report.AppendLine();
        report.AppendLine("Команда відтворення з кореня репозиторію (PowerShell):");
        report.AppendLine();
        report.AppendLine("```powershell");
        report.AppendLine("$env:DOCKER_HOST = 'npipe://./pipe/dockerDesktopLinuxEngine'");
        report.AppendLine("$env:CI = 'true'");
        report.AppendLine("dotnet test ConstantLearning.Tests/ConstantLearning.Tests.csproj --configuration Release --filter FullyQualifiedName~DictionarySearchBenchmarkTests");
        report.AppendLine("```");
        var path = Path.Combine(directory, "dictionary-search-benchmark.md");
        await File.WriteAllTextAsync(path, report.ToString());
        await File.WriteAllLinesAsync(Path.Combine(directory, "dictionary-search-samples.csv"), new[] { "request,query_length,server_ms,client_ms" }
            .Concat(samples.Select((row, index) => FormattableString.Invariant($"{index + 1},{row.Length},{row.Server:F6},{row.Client:F6}"))));
        return path;
    }

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
    }
}
