using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.HostedServices;
using ConstantLearning.Platform.Metrics;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Platform.Telegram;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NodaTime;
using Quartz;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

[assembly: WebApplicationFactoryContentRoot("ConstantLearning, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", "/app", "ConstantLearning.dll", "0")]

Environment.SetEnvironmentVariable("Telegram__BotToken", "123456:TEST_ONLY_TOKEN_NOT_VALID");
Environment.SetEnvironmentVariable("Telegram__WebhookUrl", "");
Environment.SetEnvironmentVariable("Telegram__MiniAppUrl", "https://example.invalid/");
Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Testing");
Directory.CreateDirectory("/results");
await using var factory = new LoadFactory();
factory.UseKestrel(options => options.ListenAnyIP(8888));
using var client = factory.CreateClient();
using (var scope = factory.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var now = SystemClock.Instance.GetCurrentInstant();
    var day = now.InZone(DateTimeZone.Utc).Date;
    var entries = new List<DictionaryEntry>();
    for (var i = 1; i <= 2000; i++)
    {
        var lemma = "slowo" + Suffix(i);
        var entry = new DictionaryEntry { ExternalId = $"load-{i}", Domain = "everyday", CoursePosition = i, CreatedAt = now };
        var version = new EntryVersion { Entry = entry, LemmaPl = lemma, PartOfSpeech = "noun", PronunciationUk = "слово", ExamplePl = $"To {lemma}.", ExampleUk = "Це слово.", ValidationStatus = "approved", CreatedByKind = "import", ContentHash = new string('a', 64), FrequencyRank = i, CreatedAt = now };
        db.EntryVersions.Add(version);
        db.VersionTranslations.Add(new() { VersionId = version.Id, Kind = "canonical", TextUk = $"слово {i}", Position = 0 });
        db.VersionAcceptedAnswers.Add(new() { VersionId = version.Id, TextPl = lemma, IsCanonical = true });
        entries.Add(entry);
    }
    await db.SaveChangesAsync();
    var versions = await db.EntryVersions.OrderBy(v => v.FrequencyRank).ToArrayAsync();
    for (var i = 0; i < entries.Count; i++)
    {
        entries[i].PublishedVersionId = versions[i].Id;
        db.SearchForms.Add(new SearchForm { EntryId = entries[i].Id, VersionId = versions[i].Id, Lang = "pl", FormDisplay = versions[i].LemmaPl, FormFolded = versions[i].LemmaPl, IsLemma = true, CreatedAt = now });
    }
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    for (var i = 1; i <= 100; i++)
    {
        var learner = new Learner { TelegramUserId = 910000000 + i, TimeZone = "UTC", TimeZoneConfirmed = true, DailyNewWordsLimit = 3, OnboardedAt = now, CreatedAt = now };
        db.Learners.Add(learner);
        foreach (var (entry, index) in entries.Take(1000).Select((entry, index) => (entry, index))) db.LearnerWords.Add(new LearnerWord { LearnerId = learner.Id, EntryId = entry.Id, Status = "review", FirstShownDay = index < 3 ? day : day.PlusDays(-10), DueOn = index < 3 ? day.PlusDays(1) : day, IntervalDays = index < 3 ? 1 : 2, ModelState = "{\"forgetting_rate\":0.3}", SchedulerVersion = "memorize-v1", CreatedAt = now });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
    now = SystemClock.Instance.GetCurrentInstant();
    var due = Instant.FromUnixTimeSeconds((now.ToUnixTimeSeconds() / 60 + 20) * 60);
    var time = due.InZone(DateTimeZone.Utc).TimeOfDay;
    day = now.InZone(DateTimeZone.Utc).Date;
    for (var i = 1; i <= 1000; i++)
    {
        var learner = new Learner { TelegramUserId = 920000000 + i, TimeZone = "UTC", TimeZoneConfirmed = true, DailyNewWordsLimit = 3, CanMessage = true, OnboardedAt = now, CreatedAt = now };
        db.Learners.Add(learner);
        db.ReminderSchedules.Add(new ReminderSchedule { LearnerId = learner.Id, LocalTime = time, DaysOfWeek = 127, CreatedAt = now });
        db.LearnerWords.Add(new LearnerWord { LearnerId = learner.Id, EntryId = entries[0].Id, Status = "review", FirstShownDay = day.PlusDays(-10), DueOn = day, IntervalDays = 2, ModelState = "{\"forgetting_rate\":0.3}", SchedulerVersion = "memorize-v1", CreatedAt = now });
        if (i % 100 == 0) { await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
    }
    await db.Database.ExecuteSqlRawAsync("ANALYZE dictionary_entries; ANALYZE entry_versions; ANALYZE version_translations; ANALYZE version_accepted_answers; ANALYZE search_forms; ANALYZE learners; ANALYZE learner_words; ANALYZE session_items; ANALYZE answer_events; ANALYZE reminder_schedules; ANALYZE notification_dispatches;");
    await scope.ServiceProvider.GetRequiredService<IPublishedCourseReader>().ReadAsync();
    await File.WriteAllTextAsync("/results/reminder-due.txt", due.ToString());
}
var scheduler = await factory.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
foreach (var name in new[] { "RepetitionJob", "NewWordsJob", "ContentVerification", "SummaryPlanner" }) await scheduler.DeleteJob(new JobKey(name));
await scheduler.Start();
var metrics = factory.Services.GetRequiredService<TrainerMetrics>();
Console.WriteLine("LOAD_READY http://0.0.0.0:8888");
while (true)
{
    await File.WriteAllTextAsync("/results/metrics-latest.prom", metrics.ExportPrometheus());
    await Task.Delay(TimeSpan.FromSeconds(5));
}

static string Suffix(int value)
{
    var chars = new char[4];
    for (var i = chars.Length - 1; i >= 0; i--) { chars[i] = (char)('a' + value % 26); value /= 26; }
    return new string(chars);
}

sealed class LoadFactory : WebApplicationFactory<AppDbContext>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing").UseContentRoot("/app").UseWebRoot("/app/wwwroot");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>(); services.AddSingleton<IClock>(SystemClock.Instance);
            services.RemoveAll<ITelegramClient>(); services.AddSingleton<ITelegramClient, RecordingTelegram>();
            var external = services.Where(s => s.ServiceType == typeof(IHostedService) && (s.ImplementationType == typeof(DatabaseInitializationService) || s.ImplementationType == typeof(WebhookConfigurationService) || s.ImplementationType?.Namespace?.StartsWith("Quartz", StringComparison.Ordinal) == true)).ToArray();
            foreach (var service in external) services.Remove(service);
        });
    }
}
sealed class RecordingTelegram : ITelegramClient
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public async Task SendMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.None, InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        var timestamp = SystemClock.Instance.GetCurrentInstant();
        await Gate.WaitAsync(cancellationToken);
        try { await File.AppendAllTextAsync("/results/reminder-sends.csv", $"{chatId},{timestamp}\n", cancellationToken); }
        finally { Gate.Release(); }
    }
    public Task ConfigureWebhookAsync(string url, string secretToken, IReadOnlyCollection<UpdateType> allowedUpdates, CancellationToken cancellationToken) => Task.CompletedTask;
}
