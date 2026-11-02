using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.HostedServices;
using ConstantLearning.Platform.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NodaTime;
using Testcontainers.PostgreSql;

await using var database = new PostgreSqlBuilder("postgres:16-alpine")
    .WithDatabase("miniapp_e2e").WithUsername("miniapp_test")
    .WithLabel("constant-learning.e2e", "critical-flows")
    .WithPassword(Guid.NewGuid().ToString("N")).Build();
await database.StartAsync();
Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", database.GetConnectionString());
Environment.SetEnvironmentVariable("Telegram__BotToken", "123456:TEST_ONLY_TOKEN_NOT_VALID");
Environment.SetEnvironmentVariable("Telegram__WebhookUrl", "");
Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Testing");

await using var factory = new E2eFactory();
factory.UseKestrel(options => options.ListenLocalhost(18888));
using var client = factory.CreateClient();
using (var scope = factory.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var now = scope.ServiceProvider.GetRequiredService<IClock>().GetCurrentInstant();
    var day = now.InZone(DateTimeZoneProviders.Tzdb["Europe/Warsaw"]).Date;
    var entries = new List<DictionaryEntry>();
    foreach (var (lemma, translation, position) in new[] { ("żaba", "жаба", 1), ("kot", "кіт", 2), ("dom", "будинок", 3), ("pies", "собака", 4) })
    {
        var entry = new DictionaryEntry { ExternalId = $"e2e-{position}", Domain = "everyday", CoursePosition = position };
        var version = new EntryVersion { Entry = entry, LemmaPl = lemma, PartOfSpeech = "noun", PronunciationUk = translation, ExamplePl = $"To {lemma}.", ExampleUk = $"Це {translation}.", ValidationStatus = "approved", CreatedByKind = "import", ContentHash = new string('a', 64), FrequencyRank = position };
        db.EntryVersions.Add(version);
        db.VersionTranslations.Add(new() { VersionId = version.Id, Kind = "canonical", TextUk = translation, Position = 0 });
        db.VersionAcceptedAnswers.Add(new() { VersionId = version.Id, TextPl = lemma, IsCanonical = true });
        entries.Add(entry);
        await db.SaveChangesAsync();
        entry.PublishedVersionId = version.Id;
        db.SearchForms.Add(new SearchForm { EntryId = entry.Id, VersionId = version.Id, Lang = "pl", FormDisplay = lemma, FormFolded = lemma == "żaba" ? "zaba" : lemma, IsLemma = true, CreatedAt = now });
    }
    foreach (var id in new long[] { 900000002, 900000003, 900000004 })
    {
        var learner = new Learner { TelegramUserId = id, TimeZone = "Europe/Warsaw", TimeZoneConfirmed = true, DailyNewWordsLimit = 3, OnboardedAt = now, CreatedAt = now };
        db.Learners.Add(learner);
        if (id != 900000004)
            db.LearnerWords.Add(new LearnerWord { LearnerId = learner.Id, EntryId = entries[0].Id, Status = "review", FirstShownDay = day.PlusDays(-10), DueOn = day, IntervalDays = 2, ModelState = "{\"forgetting_rate\":0.3}", SchedulerVersion = "memorize-v1" });
    }
    await db.SaveChangesAsync();
}
Console.WriteLine("E2E_READY http://127.0.0.1:18888");
await Task.Delay(Timeout.InfiniteTimeSpan);

sealed class E2eFactory : WebApplicationFactory<AppDbContext>
{
    public static readonly Instant Now = Instant.FromUtc(2026, 10, 31, 9, 0);
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var backend = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../ConstantLearning"));
        builder.UseEnvironment("Testing").UseContentRoot(backend).UseWebRoot(Path.Combine(backend, "wwwroot"));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Environment.GetEnvironmentVariable("E2E_SYSTEM_CLOCK") == "1" ? SystemClock.Instance : new FixedClock());
            var external = services.Where(s => s.ServiceType == typeof(IHostedService) && (s.ImplementationType == typeof(DatabaseInitializationService) || s.ImplementationType == typeof(WebhookConfigurationService) || s.ImplementationType?.Namespace?.StartsWith("Quartz", StringComparison.Ordinal) == true)).ToArray();
            foreach (var service in external) services.Remove(service);
        });
    }
    sealed class FixedClock : IClock { public Instant GetCurrentInstant() => Now; }
}
