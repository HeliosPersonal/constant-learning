using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConstantLearning.Tests.Fixtures;
using NodaTime;

namespace ConstantLearning.Tests.Infrastructure;

public sealed class TelegramInitDataSigner(string botToken = TelegramInitDataSigner.TestBotToken)
{
    public const string TestBotToken = "123456:TEST_ONLY_TOKEN_NOT_VALID";

    public string Sign(long telegramUserId, Instant authDate, IReadOnlyDictionary<string, string>? additionalFields = null)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["query_id"] = "test-query",
            ["user"] = JsonSerializer.Serialize(new { id = telegramUserId, first_name = "Test User" })
        };

        foreach (var field in additionalFields ?? new Dictionary<string, string>())
        {
            if (field.Key is "hash" or "auth_date" or "user")
            {
                throw new ArgumentException("Службові поля initData не можна перевизначати.", nameof(additionalFields));
            }

            fields[field.Key] = field.Value;
        }

        var dataCheckString = string.Join('\n', fields.Select(field => $"{field.Key}={field.Value}"));
        var secret = HMACSHA256.HashData("WebAppData"u8, Encoding.UTF8.GetBytes(botToken));
        fields["hash"] = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString)));
        return string.Join('&', fields.Select(field =>
            $"{Uri.EscapeDataString(field.Key)}={Uri.EscapeDataString(field.Value)}"));
    }

    public AuthenticationHeaderValue Authorization(LearnerData learner, IClock clock) =>
        new("tma", Sign(learner.TelegramUserId, clock.GetCurrentInstant()));
}
