using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ConstantLearning.Features.Identity.Auth;

public sealed class ContentEditorClaimsTransformation(IOptionsMonitor<IdentityOptions> options) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identities.FirstOrDefault(candidate =>
            candidate.IsAuthenticated && candidate.AuthenticationType == TelegramAuthentication.Scheme);
        if (identity is not null
            && long.TryParse(identity.FindFirst(TelegramAuthentication.TelegramUserIdClaim)?.Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var telegramId)
            && options.CurrentValue.ContentEditorTelegramUserIds.Contains(telegramId)
            && !identity.HasClaim(identity.RoleClaimType, TelegramAuthentication.ContentEditorRole))
        {
            identity.AddClaim(new Claim(identity.RoleClaimType, TelegramAuthentication.ContentEditorRole));
        }

        return Task.FromResult(principal);
    }
}
