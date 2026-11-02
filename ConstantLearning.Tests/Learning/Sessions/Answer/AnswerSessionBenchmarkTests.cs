using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit.Abstractions;

namespace ConstantLearning.Tests.Learning.Sessions.Answer;

public sealed class AnswerSessionBenchmarkTests(PostgresFixture postgres, ITestOutputHelper output) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task TwoHundredAnswersWithOneThousandLearnerWords_ServerP95Within500Milliseconds()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1000, session: false);
        await Db.Database.ExecuteSqlRawAsync("ANALYZE dictionary_entries; ANALYZE entry_versions; ANALYZE version_translations; ANALYZE version_accepted_answers; ANALYZE learner_words;");
        var lemmas = fixture.Entries.Select((entry, index) => (entry.Id, fixture.Versions[index].LemmaPl)).ToDictionary(pair => pair.Id, pair => pair.LemmaPl);
        await using var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(AnswerTestData.UserId, Clock.GetCurrentInstant()));

        var samples = new List<(double Server, double Client)>();
        // Перші 20 відповідей прогрівають той самий справжній шлях запису й не входять у вибірку.
        for (var sessionIndex = 0; sessionIndex < 11; sessionIndex++)
        {
            using var start = await app.Client.PostAsync("/api/v1/sessions", null);
            Assert.Equal(HttpStatusCode.Created, start.StatusCode);
            var session = JsonNode.Parse(await start.Content.ReadAsStringAsync())!;
            var sessionId = session["id"]!.GetValue<Guid>();
            var ids = await Db.SessionItems.AsNoTracking().Where(item => item.SessionId == sessionId)
                .OrderBy(item => item.Position).Select(item => new { item.Id, item.EntryId }).ToArrayAsync();
            Assert.Equal(20, ids.Length);
            if (sessionIndex == 1) app.ServerMilliseconds.Clear();
            foreach (var item in ids)
            {
                var json = new JsonObject { ["answer"] = lemmas[item.EntryId], ["dont_know"] = false }.ToJsonString();
                using var body = new StringContent(json, Encoding.UTF8, "application/json");
                var started = Stopwatch.GetTimestamp();
                using var response = await app.Client.PostAsync($"/api/v1/sessions/{sessionId}/items/{item.Id}/answer", body);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                await response.Content.ReadAsStringAsync();
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (sessionIndex > 0) samples.Add((0, elapsed));
                // Перехід закриває збережений результат і не входить у час оцінювання.
                using var continued = await app.Client.PostAsync($"/api/v1/sessions/{sessionId}/items/{item.Id}/continue", null);
                Assert.Equal(HttpStatusCode.OK, continued.StatusCode);
                await ContractAssert.ResponseAsync("continueSession", continued);
            }
        }

        var serverTimes = app.ServerMilliseconds.ToArray();
        Assert.Equal(200, serverTimes.Length);
        Assert.Equal(220, await Db.AnswerEvents.CountAsync());
        for (var index = 0; index < samples.Count; index++) samples[index] = (serverTimes[index], samples[index].Client);
        var report = await WriteReportAsync(samples);
        var p95 = Percentile(serverTimes, 0.95);
        output.WriteLine($"p95 сервера: {p95:F3} ms; звіт: {report}");
        Assert.True(p95 <= 500, $"p95 сервера {p95:F3} ms перевищує 500 ms. Звіт: {report}");
    }

    private async Task<string> WriteReportAsync(List<(double Server, double Client)> samples)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ConstantLearning.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root.FullName, "docs", "features", "personal-trainer-mvp", "test-output");
        Directory.CreateDirectory(directory);
        await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT version()", connection);
        var version = (string)(await command.ExecuteScalarAsync())!;
        var report = new StringBuilder();
        report.AppendLine("# Вимірювання затримки відповіді на картку");
        report.AppendLine();
        report.AppendLine("Фактичний прогін для [T17](../tasks/T17-answer-item.md), [PRD §6](../PRD.md#6-non-functional-requirements) та [ADR-0008](../adr/0008-keep-the-learning-session-server-side-with-per-card-idempotency.md).");
        report.AppendLine();
        report.AppendLine($"- Час UTC: {DateTimeOffset.UtcNow:O}.");
        report.AppendLine($"- ОС: {RuntimeInformation.OSDescription}; архітектура: {RuntimeInformation.ProcessArchitecture}; runtime: {RuntimeInformation.FrameworkDescription}; Release.");
        report.AppendLine($"- Логічні CPU: {Environment.ProcessorCount}; доступна .NET GC пам’ять: {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024} MiB.");
        report.AppendLine($"- PostgreSQL у локальному Docker: {version}.");
        report.AppendLine("- Фікстура: 1 000 approved-записів, 1 000 learner words до повторення; ANALYZE після заповнення. Справжні Telegram HMAC, EF Core, транзакції та PostgreSQL 16 через ASP.NET Core TestServer.");
        report.AppendLine("- 20 прогрівальних відповідей виключено; виміряно 200 послідовних відповідей у 10 сесіях по 20 карток. Запити старту session та переходу continue не входять у вибірку; паралельність 1.");
        report.AppendLine("- Серверний middleware вимірює весь HTTP pipeline: автентифікацію, БД, оцінку, збереження результату й серіалізацію; зовнішня мережа не моделюється. Перцентиль: nearest rank.");
        report.AppendLine();
        report.AppendLine("| Запити | p50 сервера, ms | p95 сервера, ms | Максимум сервера, ms | p95 клієнта, ms |");
        report.AppendLine("| ---: | ---: | ---: | ---: | ---: |");
        report.AppendLine(FormattableString.Invariant($"| {samples.Count} | {Percentile(samples.Select(row => row.Server), 0.5):F3} | {Percentile(samples.Select(row => row.Server), 0.95):F3} | {samples.Max(row => row.Server):F3} | {Percentile(samples.Select(row => row.Client), 0.95):F3} |"));
        report.AppendLine();
        report.AppendLine($"Умова p95 сервера ≤ 500 ms: {(Percentile(samples.Select(row => row.Server), 0.95) <= 500 ? "виконана" : "НЕ виконана")}. [Сирі вимірювання](session-answer-samples.csv).");
        report.AppendLine();
        report.AppendLine("```powershell");
        report.AppendLine("$env:DOCKER_HOST = 'npipe://./pipe/dockerDesktopLinuxEngine'");
        report.AppendLine("$env:CI = 'true'");
        report.AppendLine("dotnet test ConstantLearning.Tests/ConstantLearning.Tests.csproj --configuration Release --filter FullyQualifiedName~AnswerSessionBenchmarkTests");
        report.AppendLine("```");
        var path = Path.Combine(directory, "session-answer-benchmark.md");
        await File.WriteAllTextAsync(path, report.ToString());
        await File.WriteAllLinesAsync(Path.Combine(directory, "session-answer-samples.csv"), new[] { "request,server_ms,client_ms" }
            .Concat(samples.Select((row, index) => FormattableString.Invariant($"{index + 1},{row.Server:F6},{row.Client:F6}"))));
        return path;
    }

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
    }
}
