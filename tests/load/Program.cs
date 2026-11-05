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
    // Когорта для навантажувального тесту скидання (T09): кожен learner має повний граф найгіршого
    // випадку з [PRD §6](../../docs/features/reset-progress/PRD.md#6-non-functional-requirements) — 2000 слів,
    // 20000 відповідей, заняття з items і content reports, привʼязані до відповідей. reset.js скидає їх по черзі.
    day = now.InZone(DateTimeZone.Utc).Date;
    for (var i = 1; i <= 50; i++)
    {
        var resetLearner = new Learner { TelegramUserId = 930000000 + i, TimeZone = "UTC", TimeZoneConfirmed = true, DailyNewWordsLimit = 3, OnboardedAt = now, CreatedAt = now };
        db.Learners.Add(resetLearner);
        foreach (var entry in entries) db.LearnerWords.Add(new LearnerWord { LearnerId = resetLearner.Id, EntryId = entry.Id, Status = "review", FirstShownDay = day.PlusDays(-10), DueOn = day, IntervalDays = 2, ModelState = "{\"forgetting_rate\":0.3}", SchedulerVersion = "memorize-v1", CreatedAt = now });
        var resetSession = new LearningSession { LearnerId = resetLearner.Id, LearnerDay = day, EndsAt = now.Plus(Duration.FromHours(1)), Status = "completed", Kind = "plan", CompletedAt = now, CreatedAt = now };
        db.LearningSessions.Add(resetSession);
        var resetItems = new List<SessionItem>();
        for (short p = 0; p < 20; p++) { var item = new SessionItem { SessionId = resetSession.Id, EntryId = entries[p].Id, Position = p, Kind = "review", State = "graded", AnsweredAt = now, CreatedAt = now }; resetItems.Add(item); db.SessionItems.Add(item); }
        await db.SaveChangesAsync();
        var linkedAnswers = new List<AnswerEvent>();
        for (var r = 0; r < 3; r++) { var linked = new AnswerEvent { LearnerId = resetLearner.Id, EntryId = entries[r].Id, VersionId = versions[r].Id, SessionItemId = resetItems[r].Id, ExerciseKind = "recall", Outcome = "correct", RawAnswer = "slowo", NormalizedAnswer = "slowo", AnsweredAt = now, LearnerDay = day, TimeZone = "UTC", SchedulerVersion = "memorize-v1", StateBefore = "{\"forgetting_rate\":0.3}", CreatedAt = now }; linkedAnswers.Add(linked); db.AnswerEvents.Add(linked); }
        await db.SaveChangesAsync();
        foreach (var linked in linkedAnswers) db.ContentReports.Add(new ContentReport { LearnerId = resetLearner.Id, EntryId = linked.EntryId, VersionId = linked.VersionId, AnswerEventId = linked.Id, Origin = "answer", Status = "open", CreatedAt = now });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var answerBatch = new List<AnswerEvent>(5000);
        for (var a = 3; a < 20000; a++)
        {
            var index = a % entries.Count;
            answerBatch.Add(new AnswerEvent { LearnerId = resetLearner.Id, EntryId = entries[index].Id, VersionId = versions[index].Id, ExerciseKind = "recall", Outcome = a % 4 == 0 ? "mistake_typo" : "correct", RawAnswer = "slowo", NormalizedAnswer = "slowo", AnsweredAt = now, LearnerDay = day, TimeZone = "UTC", SchedulerVersion = "memorize-v1", StateBefore = "{\"forgetting_rate\":0.3}", CreatedAt = now });
            if (answerBatch.Count == 5000) { db.AnswerEvents.AddRange(answerBatch); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); answerBatch.Clear(); }
        }
        if (answerBatch.Count > 0) { db.AnswerEvents.AddRange(answerBatch); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
    }
    await File.WriteAllTextAsync("/results/reset-cohort.txt", "50 learners 930000001..930000050, 2000 words / 20000 answers each");
    // Когорта для навантажувального тесту старту extra (T12 unlimited-learning): план дня виконано (нові
    // слова відкладено на завтра, повторень на сьогодні немає), але лишилися непоказані слова курсу й
    // придатні до практики слова. 940000001..940000050 — extra-word, 940000051..940000100 — extra-practice.
    // Межі [PRD §6](../../docs/features/unlimited-learning/PRD.md#6-non-functional-requirements):
    // старт extra-слова p95 ≤ 500 ms, старт практики p95 ≤ 800 ms. extra-start.js бʼє ці когорти.
    day = now.InZone(DateTimeZone.Utc).Date;
    var extraDeferred = now.Plus(Duration.FromDays(1));
    for (var i = 1; i <= 100; i++)
    {
        var extraLearner = new Learner { TelegramUserId = 940000000 + i, TimeZone = "UTC", TimeZoneConfirmed = true, DailyNewWordsLimit = 3, NewWordsAvailableFrom = extraDeferred, OnboardedAt = now, CreatedAt = now };
        db.Learners.Add(extraLearner);
        foreach (var entry in entries.Take(1000)) db.LearnerWords.Add(new LearnerWord { LearnerId = extraLearner.Id, EntryId = entry.Id, Status = "review", FirstShownDay = day.PlusDays(-10), DueOn = day.PlusDays(1), IntervalDays = 2, ModelState = "{\"forgetting_rate\":0.3}", SchedulerVersion = "memorize-v1", CreatedAt = now });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
    await File.WriteAllTextAsync("/results/extra-cohort.txt", "100 learners 940000001..940000100 (план дня виконано); 940000001..50 extra-word, 940000051..100 extra-practice");
    // Когорта для навантажувального тесту перемикання мови (T17 multi-target-languages): en увімкнено як
    // тестову мову (у проді лишається вимкненою до готовності контенту, OQ-7), засіяно en base course і
    // 50 learner, що ведуть pl (активна) + en з непорожнім обліком у кожній. language-switch.js бʼє цю
    // когорту. Межі [PRD §6](../../docs/features/multi-target-languages/PRD.md#6-non-functional-requirements):
    // p95 перемикання активної мови ≤ 1500 ms, p95 відкриття «Сьогодні» ≤ 1500 ms.
    var english = await db.Set<TargetLanguage>().SingleAsync(l => l.Code == "en");
    english.IsEnabled = true;
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    var enEntries = new List<DictionaryEntry>();
    var enVersions = new List<EntryVersion>();
    for (var i = 1; i <= 200; i++)
    {
        var lemma = "worten" + Suffix(i);
        var entry = new DictionaryEntry { ExternalId = $"load-en-{i}", TargetLanguage = "en", Domain = "everyday", CoursePosition = i, CreatedAt = now };
        var version = new EntryVersion { Entry = entry, LemmaPl = lemma, PartOfSpeech = "noun", PronunciationUk = "слово", ExamplePl = $"The {lemma}.", ExampleUk = "Це слово.", ValidationStatus = "approved", CreatedByKind = "import", ContentHash = new string('b', 64), FrequencyRank = i, CreatedAt = now };
        db.EntryVersions.Add(version);
        db.VersionTranslations.Add(new() { VersionId = version.Id, Kind = "canonical", TextUk = $"слово en {i}", Position = 0 });
        db.VersionAcceptedAnswers.Add(new() { VersionId = version.Id, TextPl = lemma, IsCanonical = true });
        enEntries.Add(entry);
        enVersions.Add(version);
    }
    await db.SaveChangesAsync();
    for (var i = 0; i < enEntries.Count; i++)
    {
        enEntries[i].PublishedVersionId = enVersions[i].Id;
        db.SearchForms.Add(new SearchForm { EntryId = enEntries[i].Id, VersionId = enVersions[i].Id, Lang = "en", FormDisplay = enVersions[i].LemmaPl, FormFolded = enVersions[i].LemmaPl, IsLemma = true, CreatedAt = now });
    }
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    day = now.InZone(DateTimeZone.Utc).Date;
    for (var i = 1; i <= 50; i++)
    {
        var learner = new Learner { TelegramUserId = 950000000 + i, TimeZone = "UTC", TimeZoneConfirmed = true, DailyNewWordsLimit = 3, ActiveTargetLanguage = "pl", OnboardedAt = now, CreatedAt = now };
        db.Learners.Add(learner);
        db.Set<LearnerLanguageState>().Add(new LearnerLanguageState { LearnerId = learner.Id, TargetLanguage = "pl", DailyNewWordsLimit = 3, CreatedAt = now });
        db.Set<LearnerLanguageState>().Add(new LearnerLanguageState { LearnerId = learner.Id, TargetLanguage = "en", DailyNewWordsLimit = 3, CreatedAt = now });
        foreach (var (entry, index) in entries.Take(200).Select((entry, index) => (entry, index))) db.LearnerWords.Add(new LearnerWord { LearnerId = learner.Id, EntryId = entry.Id, TargetLanguage = "pl", Status = "review", FirstShownDay = index < 3 ? day : day.PlusDays(-10), DueOn = index < 3 ? day.PlusDays(1) : day, IntervalDays = index < 3 ? 1 : 2, ModelState = "{\"forgetting_rate\":0.3}", SchedulerVersion = "memorize-v1", CreatedAt = now });
        foreach (var (entry, index) in enEntries.Take(200).Select((entry, index) => (entry, index))) db.LearnerWords.Add(new LearnerWord { LearnerId = learner.Id, EntryId = entry.Id, TargetLanguage = "en", Status = "review", FirstShownDay = index < 3 ? day : day.PlusDays(-10), DueOn = index < 3 ? day.PlusDays(1) : day, IntervalDays = index < 3 ? 1 : 2, ModelState = "{\"forgetting_rate\":0.3}", SchedulerVersion = "memorize-v1", CreatedAt = now });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
    await File.WriteAllTextAsync("/results/language-cohort.txt", "50 learners 950000001..950000050 (pl active + en), 200 words each language");
    await db.Database.ExecuteSqlRawAsync("ANALYZE dictionary_entries; ANALYZE entry_versions; ANALYZE version_translations; ANALYZE version_accepted_answers; ANALYZE search_forms; ANALYZE learners; ANALYZE learner_words; ANALYZE session_items; ANALYZE answer_events; ANALYZE content_reports; ANALYZE reminder_schedules; ANALYZE notification_dispatches;");
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
