using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ConstantLearning.Platform.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConstantLearning.Features.Identity.Auth;

public sealed class TelegramAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TelegramInitDataValidator validator,
    AppDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers.Authorization;
        if (headers.Count == 0)
        {
            return AuthenticateResult.NoResult();
        }

        if (headers.Count != 1 || !AuthenticationHeaderValue.TryParse(headers[0], out var header)
            || !string.Equals(header.Scheme, TelegramAuthentication.Scheme, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter)
            || validator.Validate(header.Parameter) is not { } telegram)
        {
            Logger.LogInformation("Автентифікацію Telegram відхилено: {Reason}", "invalid_init_data");
            return AuthenticateResult.Fail("auth.invalid_init_data");
        }

        var learnerId = await db.Learners.AsNoTracking()
            .Where(learner => learner.TelegramUserId == telegram.UserId)
            .Select(learner => (Guid?)learner.Id)
            .SingleOrDefaultAsync(Context.RequestAborted);
        var claims = new List<Claim>
        {
            new(TelegramAuthentication.TelegramUserIdClaim, telegram.UserId.ToString(CultureInfo.InvariantCulture))
        };
        if (telegram.FirstName is not null)
        {
            claims.Add(new Claim(ClaimTypes.GivenName, telegram.FirstName));
        }

        if (learnerId is { } id)
        {
            claims.Add(new Claim(TelegramAuthentication.LearnerIdClaim, id.ToString()));
        }

        Logger.LogInformation("Автентифікацію Telegram підтверджено для {LearnerId}", learnerId);
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = TelegramAuthentication.Scheme;
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Response.WriteAsJsonAsync(new
        {
            code = "auth.invalid_init_data",
            message = "Перевідкрийте тренер у Telegram."
        }, Context.RequestAborted);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Logger.LogInformation("Редакторський доступ відхилено для {LearnerId}", Context.User.GetLearnerId());
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Response.WriteAsJsonAsync(new
        {
            code = "editor.role_required",
            message = "Розгляд повідомлень доступний лише редактору."
        }, Context.RequestAborted);
    }
}
