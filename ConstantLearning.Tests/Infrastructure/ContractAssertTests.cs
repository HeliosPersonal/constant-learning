using System.Net;
using System.Text;
using AwesomeAssertions;
using Xunit.Sdk;

namespace ConstantLearning.Tests.Infrastructure;

public sealed class ContractAssertTests
{
    private const string ValidToday = """
        {"learner_day":"2026-10-31","due_review_count":7,"recalled_words_count":12,
         "completed_sessions_last_7_days":3,"has_active_session":false,
         "course_state":"in_progress","course_replenished_notice":false,"new_words_available_today":true,
         "extra_actions":{"plan_done":false,"extra_new_word":{"available":false,"reason":"plan_not_done"},
         "extra_practice":{"available":false,"reason":"plan_not_done"}}}
        """;

    [Fact]
    public async Task Response_ValidatesAnHttpResponseAgainstOpenApi()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidToday, Encoding.UTF8, "application/json")
        };
        await ContractAssert.ResponseAsync("getToday", response);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("\"7\"")]
    [InlineData("-1")]
    public void Response_RejectsMissingRequiredFieldsAndInvalidCounterTypes(string invalidCounter)
    {
        var json = invalidCounter == "{}" ? "{}" : ValidToday.Replace("\"due_review_count\":7", $"\"due_review_count\":{invalidCounter}");
        var validate = () => ContractAssert.Response("getToday", 200, json);
        validate.Should().Throw<XunitException>().WithMessage("*OpenAPI getToday/200*");
    }

    [Fact]
    public void Response_ResolvesSharedErrorsAndEnforcesEnumsFormatsAndUnknownFields()
    {
        ContractAssert.Response("getToday", 401, """{"code":"auth.invalid_init_data","message":"Перевідкрийте тренер."}""");
        foreach (var invalid in new[]
                 {
                     ValidToday.Replace("in_progress", "unknown"),
                     ValidToday.Replace("2026-10-31", "31.10.2026"),
                     ValidToday.Replace("\"course_replenished_notice\":false", "\"course_replenished_notice\":false,\"extra\":1")
                 })
        {
            var validate = () => ContractAssert.Response("getToday", 200, invalid);
            validate.Should().Throw<XunitException>();
        }
    }

    [Fact]
    public void Response_ResolvesNestedSchemasAndAllowsDocumentedNulls()
    {
        ContractAssert.Response("getCurrentLearner", 200, """
            {"id":"0192f0a0-0000-7000-8000-000000000001","time_zone":"Europe/Warsaw",
             "time_zone_confirmed":true,"daily_new_words_limit":5,"reminder":null,"onboarded_at":null,"active_target_language":null}
            """);
        var validate = () => ContractAssert.Response("getCurrentLearner", 200, """
            {"id":"invalid","time_zone":"Europe/Warsaw","time_zone_confirmed":true,
             "daily_new_words_limit":4,"reminder":null,"onboarded_at":null}
            """);
        validate.Should().Throw<XunitException>();
    }

    [Fact]
    public void EventRows_ReadRequiredFieldsTypesAndEnumsFromMarkdown()
    {
        const string verification = """
            {"id":"0192f0a0-0000-7000-8000-000000000001",
             "content_report_id":"0192f0a0-0000-7000-8000-000000000002","status":"pending",
             "attempts":0,"next_attempt_at":"2026-10-31T09:00:00Z"}
            """;
        const string notification = """
            {"id":"0192f0a0-0000-7000-8000-000000000001","recipient_telegram_user_id":900000001,
             "learner_id":null,"kind":"editor_digest","local_date":"2026-10-31",
             "due_at":"2026-10-31T09:00:00Z","status":"pending"}
            """;
        ContractAssert.EventRow("verification_jobs", verification);
        ContractAssert.EventRow("notification_dispatches", notification);

        foreach (var invalid in new[]
                 {
                     verification.Replace("pending", "sending"),
                     verification.Replace("\"attempts\":0", "\"attempts\":\"zero\""),
                     verification.Replace("2026-10-31T09:00:00Z", "yesterday"),
                     "{}"
                 })
        {
            var validate = () => ContractAssert.EventRow("verification_jobs", invalid);
            validate.Should().Throw<XunitException>();
        }

        var invalidNotification = () => ContractAssert.EventRow("notification_dispatches",
            notification.Replace("editor_digest", "unknown"));
        invalidNotification.Should().Throw<XunitException>();
    }
}
