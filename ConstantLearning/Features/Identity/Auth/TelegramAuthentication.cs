using System.Security.Claims;

namespace ConstantLearning.Features.Identity.Auth;

public static class TelegramAuthentication
{
    public const string Scheme = "tma";
    public const string TelegramUserIdClaim = "telegram_user_id";
    public const string LearnerIdClaim = "learner_id";
    public const string ContentEditorPolicy = "ContentEditor";
    public const string ContentEditorRole = "ContentEditor";

    public static Guid? GetLearnerId(this ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true
        && Guid.TryParse(principal.FindFirstValue(LearnerIdClaim), out var id) ? id : null;
}

public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    // Значення надходять із секретної конфігурації, а не з даних запиту.
    public long[] ContentEditorTelegramUserIds { get; set; } = [];
}
