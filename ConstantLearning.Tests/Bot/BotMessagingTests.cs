using System.Net;
using System.Net.Http.Json;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Tests.Bot;

public sealed class BotMessagingTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task BlockingAndUnblocking_UpdateOnlyPrivateChatOwner_AndPreserveReminder()
    {
        var owner = new Learner { TelegramUserId = 900000001, TimeZone = "Europe/Warsaw", CanMessage = true,
            DailyNewWordsLimit = 5, OnboardedAt = Clock.GetCurrentInstant(), CreatedAt = Clock.GetCurrentInstant() };
        var actor = new Learner { TelegramUserId = 999999999, TimeZone = "Europe/Warsaw", CanMessage = true,
            DailyNewWordsLimit = 3, CreatedAt = Clock.GetCurrentInstant() };
        Db.Learners.AddRange(owner, actor);
        Db.ReminderSchedules.Add(new ReminderSchedule { LearnerId = owner.Id, Learner = owner,
            LocalTime = new NodaTime.LocalTime(19, 0), DaysOfWeek = 127, CreatedAt = Clock.GetCurrentInstant() });
        await Db.SaveChangesAsync();
        await using var app = await BotTestApplication.StartAsync(_postgres.ConnectionString, Clock);

        foreach (var (status, expected) in new[] { ("kicked", false), ("kicked", false), ("member", true), ("left", false) })
        {
            using var response = await BotWebhookTests.PostAsync(app.Client, BotWebhookTests.MembershipUpdate(status));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await ContractAssert.ResponseAsync("receiveTelegramUpdate", response);
            Assert.Equal(expected, await Db.Learners.AsNoTracking().Where(value => value.Id == owner.Id)
                .Select(value => value.CanMessage).SingleAsync());
            Assert.True(await Db.Learners.AsNoTracking().Where(value => value.Id == actor.Id).Select(value => value.CanMessage).SingleAsync());
            Assert.Equal(127, (await Db.ReminderSchedules.AsNoTracking().SingleAsync()).DaysOfWeek);
        }
    }

    [DockerFact]
    public async Task Start_ForNewIdentity_DoesNotCompleteOnboarding_ThenOnboardingEnablesMessaging()
    {
        await using var app = await BotTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        using var start = await BotWebhookTests.PostAsync(app.Client, BotWebhookTests.StartUpdate);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Single(app.Telegram.Messages);
        Assert.Empty(await Db.Learners.ToListAsync());

        app.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(900000001, Clock.GetCurrentInstant()));
        using var profile = await app.Client.GetAsync("/api/v1/learners/me");
        Assert.Equal(HttpStatusCode.NotFound, profile.StatusCode);
        using var onboarding = await app.Client.PostAsJsonAsync("/api/v1/learners/me/onboarding",
            new { daily_new_words_limit = 5, time_zone = "Europe/Warsaw" });
        Assert.Equal(HttpStatusCode.Created, onboarding.StatusCode);
        await ContractAssert.ResponseAsync("completeOnboarding", onboarding);
        var learner = await Db.Learners.AsNoTracking().SingleAsync();
        Assert.True(learner.CanMessage);
        Assert.NotNull(learner.OnboardedAt);
    }
}
