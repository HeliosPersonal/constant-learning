using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit.Abstractions;
using static ConstantLearning.Tests.Learning.Today.TodayTestData;

namespace ConstantLearning.Tests.Learning.Today;

public sealed class TodayBenchmarkTests(PostgresFixture postgres, ITestOutputHelper output)
    : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Today_200RequestsWith1000LearnerWords_ServerP95Within300Milliseconds()
    {
        var learner = Learner(Clock);
        Db.Add(learner);
        var course = await CourseAsync(Db, 1000);
        Db.LearnerWords.AddRange(course.Select((entry, index) => Word(learner, entry,
            index < 400 ? "recalled" : "review", 10, index < 700 ? 0 : 1)));
        Db.AnswerEvents.AddRange(course.Take(100).Select(entry => Attempt(learner, entry, Clock)));
        Db.LearningSessions.AddRange(Enumerable.Range(0, 3).Select(index =>
            Completed(learner, Clock.GetCurrentInstant() - NodaTime.Duration.FromHours(index * 24))));
        await Db.SaveChangesAsync();
        await Db.Database.ExecuteSqlRawAsync("ANALYZE learners; ANALYZE learner_words; ANALYZE answer_events; ANALYZE learning_sessions; ANALYZE dictionary_entries; ANALYZE entry_versions; ANALYZE version_translations;");
        await using var app = await TodayTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        TodayTests.Authenticate(app, UserId, Clock);
        for (var index = 0; index < 25; index++)
        {
            using var warmup = await app.Client.GetAsync("/api/v1/today");
            Assert.Equal(HttpStatusCode.OK, warmup.StatusCode);
            await warmup.Content.ReadAsStringAsync();
        }
        app.ServerMilliseconds.Clear();
        var clientTimes = new List<double>();
        for (var index = 0; index < 200; index++)
        {
            var start = Stopwatch.GetTimestamp();
            using var response = await app.Client.GetAsync("/api/v1/today");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            clientTimes.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            var body = JsonNode.Parse(content)!;
            Assert.Equal(600, body["due_review_count"]!.GetValue<int>());
            Assert.Equal(400, body["recalled_words_count"]!.GetValue<int>());
            Assert.Equal(3, body["completed_sessions_last_7_days"]!.GetValue<int>());
            if (index == 0) await ContractAssert.ResponseAsync("getToday", response);
        }
        var serverTimes = app.ServerMilliseconds.ToArray();
        Assert.Equal(200, serverTimes.Length);
        var report = await WriteReportAsync(serverTimes, clientTimes);
        var p95 = Percentile(serverTimes, 0.95);
        output.WriteLine($"p95 сервера: {p95:F3} ms; звіт: {report}");
        Assert.True(p95 <= 300, $"p95 сервера {p95:F3} ms перевищує 300 ms. Звіт: {report}");
    }

    private async Task<string> WriteReportAsync(double[] serverTimes, List<double> clientTimes)
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
        report.AppendLine("# Вимірювання затримки екрана «Сьогодні»");
        report.AppendLine();
        report.AppendLine("Фактичний прогін для [T19](../tasks/T19-today-api.md), [AC-28](../PRD.md#ac-28---happy-path), [AC-29](../PRD.md#ac-29---authorization) та контракту [getToday](../contracts/openapi.yaml).");
        report.AppendLine();
        report.AppendLine($"- Час UTC: {DateTimeOffset.UtcNow:O}.");
        report.AppendLine($"- ОС процесу: {RuntimeInformation.OSDescription}; архітектура: {RuntimeInformation.ProcessArchitecture}.");
        report.AppendLine($"- CPU: {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "не надано середовищем"}; доступні логічні CPU: {Environment.ProcessorCount}.");
        report.AppendLine($"- Пам’ять, доступна .NET GC: {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024} MiB; runtime: {RuntimeInformation.FrameworkDescription}; конфігурація: Release.");
        report.AppendLine($"- PostgreSQL у локальному Docker: {databaseVersion}.");
        report.AppendLine("- HTTP через ASP.NET Core TestServer; справжня перевірка Telegram HMAC, scoped EF Core й PostgreSQL. Зовнішню мережеву затримку не вимірює.");
        report.AppendLine("- Фікстура: 1 000 learner words, 1 000 опублікованих approved-записів і перекладів, 100 спроб поточної доби, 3 завершені session. Лічильники кожної відповіді перевірено: 600 due, 400 recalled, 3 completed.");
        report.AppendLine("- ANALYZE після заповнення; 25 прогрівальних запитів виключено; 200 послідовних GET, паралельність 1.");
        report.AppendLine("- Час сервера: middleware від початку HTTP до повернення pipeline, включно з автентифікацією, блокуванням learner, транзакцією, запитами БД, серіалізацією. Перцентилі: nearest rank.");
        report.AppendLine();
        report.AppendLine("| Запити | p50 сервера, ms | p95 сервера, ms | Максимум сервера, ms | p95 клієнта, ms |");
        report.AppendLine("| ---: | ---: | ---: | ---: | ---: |");
        report.AppendLine(FormattableString.Invariant($"| {serverTimes.Length} | {Percentile(serverTimes, 0.5):F3} | {Percentile(serverTimes, 0.95):F3} | {serverTimes.Max():F3} | {Percentile(clientTimes, 0.95):F3} |"));
        report.AppendLine();
        report.AppendLine($"Умова p95 сервера ≤ 300 ms: {(Percentile(serverTimes, 0.95) <= 300 ? "виконана" : "НЕ виконана")}. [Сирі вимірювання](today-samples.csv).");
        report.AppendLine();
        report.AppendLine("Команда відтворення з кореня репозиторію (PowerShell):");
        report.AppendLine();
        report.AppendLine("```powershell");
        report.AppendLine("$env:DOCKER_HOST = 'npipe://./pipe/dockerDesktopLinuxEngine'");
        report.AppendLine("$env:CI = 'true'");
        report.AppendLine("dotnet test ConstantLearning.Tests/ConstantLearning.Tests.csproj --configuration Release --filter FullyQualifiedName~ConstantLearning.Tests.Learning.Today.TodayBenchmarkTests");
        report.AppendLine("```");
        var path = Path.Combine(directory, "today-benchmark.md");
        await File.WriteAllTextAsync(path, report.ToString());
        await File.WriteAllLinesAsync(Path.Combine(directory, "today-samples.csv"), new[] { "request,server_ms,client_ms" }
            .Concat(serverTimes.Select((server, index) => FormattableString.Invariant($"{index + 1},{server:F6},{clientTimes[index]:F6}"))));
        return path;
    }

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
    }
}
