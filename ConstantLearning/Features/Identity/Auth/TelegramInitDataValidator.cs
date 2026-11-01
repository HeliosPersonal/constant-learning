using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConstantLearning.Configuration;
using Microsoft.Extensions.Options;
using NodaTime;

namespace ConstantLearning.Features.Identity.Auth;

public sealed record TelegramIdentity(long UserId, string? FirstName);

public sealed class TelegramInitDataValidator(IOptionsMonitor<TelegramOptions> options, IClock clock)
{
    public TelegramIdentity? Validate(string initData)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in initData.Split('&'))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0 || !HasValidEncoding(pair))
            {
                return null;
            }

            var key = Decode(pair[..separator]);
            var value = Decode(pair[(separator + 1)..]);
            if (key.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_')
                || value.Contains('\n') || value.Contains('\r') || !fields.TryAdd(key, value))
            {
                return null;
            }
        }

        if (!fields.Remove("hash", out var hash) || hash.Length != 64
            || !fields.TryGetValue("auth_date", out var authDate)
            || !long.TryParse(authDate, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || !fields.TryGetValue("user", out var userJson))
        {
            return null;
        }

        var now = clock.GetCurrentInstant();
        var nowSeconds = now.ToUnixTimeSeconds();
        if (seconds > nowSeconds || seconds < nowSeconds - 86400
            || now - Instant.FromUnixTimeSeconds(seconds) > Duration.FromDays(1))
        {
            return null;
        }

        byte[] suppliedHash;
        try
        {
            suppliedHash = Convert.FromHexString(hash);
        }
        catch (FormatException)
        {
            return null;
        }

        var botToken = options.CurrentValue.BotToken;
        if (string.IsNullOrWhiteSpace(botToken))
        {
            return null;
        }

        var secret = HMACSHA256.HashData("WebAppData"u8, Encoding.UTF8.GetBytes(botToken));
        var dataCheckString = string.Join('\n', fields.Select(field => $"{field.Key}={field.Value}"));
        var expectedHash = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(userJson);
            var user = document.RootElement;
            if (user.ValueKind != JsonValueKind.Object
                || user.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() > 1)
                || !user.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number
                || !id.TryGetInt64(out var userId) || userId <= 0)
            {
                return null;
            }

            string? firstName = null;
            if (user.TryGetProperty("first_name", out var name))
            {
                if (name.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                firstName = name.GetString();
            }

            return new TelegramIdentity(userId, firstName);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));

    private static bool HasValidEncoding(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length || !Uri.IsHexDigit(value[index + 1]) || !Uri.IsHexDigit(value[index + 2]))
            {
                return false;
            }

            index += 2;
        }

        return true;
    }
}
