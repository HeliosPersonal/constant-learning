using System.Security.Claims;
using ConstantLearning.Features.Identity.Auth;
using Microsoft.Extensions.Options;
using Moq;

namespace ConstantLearning.Tests.Identity.Auth;

public sealed class ContentEditorClaimsTransformationTests
{
    [Theory]
    [InlineData("tma", "123", true)]
    [InlineData("tma", "456", false)]
    [InlineData("tma", "invalid", false)]
    [InlineData("another-scheme", "123", false)]
    [InlineData(null, "123", false)]
    public async Task TransformAsync_GrantsOnlyConfiguredVerifiedIdentityAndIsIdempotent(string? scheme, string id, bool allowed)
    {
        var options = new Mock<IOptionsMonitor<IdentityOptions>>();
        options.SetupGet(value => value.CurrentValue).Returns(new IdentityOptions { ContentEditorTelegramUserIds = [123] });
        var transformation = new ContentEditorClaimsTransformation(options.Object);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(TelegramAuthentication.TelegramUserIdClaim, id)], scheme));

        await transformation.TransformAsync(principal);
        await transformation.TransformAsync(principal);

        Assert.Equal(allowed, principal.IsInRole(TelegramAuthentication.ContentEditorRole));
        Assert.Equal(allowed ? 1 : 0, principal.Claims.Count(claim => claim.Type == ClaimTypes.Role));
    }
}
