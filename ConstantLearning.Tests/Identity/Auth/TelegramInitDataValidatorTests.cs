using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ConstantLearning.Configuration;
using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.Extensions.Options;
using Moq;
using NodaTime;
using NodaTime.Testing;

namespace ConstantLearning.Tests.Identity.Auth;

public sealed class TelegramInitDataValidatorTests
{
    private static readonly Instant Now = Instant.FromUnixTimeSeconds(1793437200);
    private readonly FakeClock _clock = new(Now);
    private readonly TelegramOptions _options = new() { BotToken = TelegramInitDataSigner.TestBotToken };

    [Fact]
    public void Validate_AcceptsIndependentFixedHmacVectorIncludingSignatureAndUnicode()
    {
        // Вектор обчислено незалежно через Python hmac/hashlib, без тестового C# signer.
        const string vector = "auth_date=1793437200&query_id=fixed-vector&user=%7B%22id%22%3A9876543210123%2C%22first_name%22%3A%22%D0%9E%D0%BB%D0%B5%D0%BD%D0%B0+%2B+Anna%22%7D&signature=signed-extra-field&hash=63530eb722aa0291ac004fd67ed01e0ace0dfbfc57e21bd1f80c6fc1ccab8dec";
        Assert.Equal(new TelegramIdentity(9876543210123, "Олена + Anna"), CreateValidator().Validate(vector));
        Assert.Null(CreateValidator().Validate(vector.Replace("signed-extra-field", "changed", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(86399, true)]
    [InlineData(86400, true)]
    [InlineData(86401, false)]
    [InlineData(-1, false)]
    public void Validate_EnforcesInclusive24HourWindow(int ageSeconds, bool valid)
    {
        var data = new TelegramInitDataSigner().Sign(123, Now - Duration.FromSeconds(ageSeconds));
        Assert.Equal(valid, CreateValidator().Validate(data) is not null);
    }

    [Fact]
    public void Validate_ChecksFreshnessOnEveryRequestAndBotTokenRotation()
    {
        var data = new TelegramInitDataSigner().Sign(123, Now);
        var validator = CreateValidator();
        Assert.NotNull(validator.Validate(data));
        _clock.Advance(Duration.FromDays(1) + Duration.FromSeconds(1));
        Assert.Null(validator.Validate(data));
        _clock.Reset(Now);
        _options.BotToken = "rotated-token";
        Assert.Null(validator.Validate(data));
        _options.BotToken = "";
        Assert.Null(validator.Validate(data));
    }

    [Fact]
    public void Validate_RejectsImmediatelyAfter24HourBoundary()
    {
        var data = new TelegramInitDataSigner().Sign(123, Now);
        _clock.Advance(Duration.FromDays(1) + Duration.FromMilliseconds(1));
        Assert.Null(CreateValidator().Validate(data));
    }

    [Theory]
    [InlineData("")]
    [InlineData("auth_date=1793437200")]
    [InlineData("hash=bad&auth_date=1793437200&user={}")]
    [InlineData("missing-equals")]
    [InlineData("=value")]
    [InlineData("query_id=%ZZ")]
    [InlineData("query_id=%A")]
    public void Validate_RejectsMalformedQuery(string data) => Assert.Null(CreateValidator().Validate(data));

    [Theory]
    [InlineData("&auth_date=1793437200")]
    [InlineData("&%61uth_date=1793437200")]
    [InlineData("&user=%7B%22id%22%3A999%7D")]
    [InlineData("&hash=0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("&extra=unsigned")]
    [InlineData("&")]
    public void Validate_RejectsDuplicateOrUnsignedFields(string suffix)
    {
        var data = new TelegramInitDataSigner().Sign(123, Now);
        Assert.Null(CreateValidator().Validate(data + suffix));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"id\":\"123\"}")]
    [InlineData("{\"id\":0}")]
    [InlineData("{\"id\":-1}")]
    [InlineData("{\"id\":1.5}")]
    [InlineData("{\"id\":9223372036854775808}")]
    [InlineData("{\"id\":123,\"id\":456}")]
    [InlineData("{\"id\":123,\"first_name\":{}}")]
    public void Validate_RejectsSignedMalformedUser(string user) =>
        Assert.Null(CreateValidator().Validate(SignRaw(user)));

    [Theory]
    [InlineData("9223372036854775807")]
    [InlineData("-9223372036854775808")]
    [InlineData("invalid")]
    [InlineData("1793437200.0")]
    public void Validate_RejectsSignedInvalidAuthDate(string date) =>
        Assert.Null(CreateValidator().Validate(SignRaw("{\"id\":123}", date)));

    [Fact]
    public void Validate_RejectsTamperedHashAndWrongBot()
    {
        var data = new TelegramInitDataSigner().Sign(123, Now);
        var hashStart = data.IndexOf("hash=", StringComparison.Ordinal) + 5;
        var tampered = data[..hashStart] + (data[hashStart] == '0' ? '1' : '0') + data[(hashStart + 1)..];
        Assert.Null(CreateValidator().Validate(tampered));
        Assert.Null(CreateValidator().Validate(new TelegramInitDataSigner("another-bot").Sign(123, Now)));
    }

    private TelegramInitDataValidator CreateValidator()
    {
        var monitor = new Mock<IOptionsMonitor<TelegramOptions>>();
        monitor.SetupGet(value => value.CurrentValue).Returns(() => _options);
        return new TelegramInitDataValidator(monitor.Object, _clock);
    }

    private static string SignRaw(string user, string? date = null)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = date ?? Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["user"] = user
        };
        var secret = HMACSHA256.HashData("WebAppData"u8, Encoding.UTF8.GetBytes(TelegramInitDataSigner.TestBotToken));
        fields["hash"] = Convert.ToHexStringLower(HMACSHA256.HashData(secret,
            Encoding.UTF8.GetBytes(string.Join('\n', fields.Select(field => $"{field.Key}={field.Value}")))));
        return string.Join('&', fields.Select(field => $"{field.Key}={Uri.EscapeDataString(field.Value)}"));
    }
}
