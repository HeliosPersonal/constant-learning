using ConstantLearning.Configuration;
using ConstantLearning.Features.Identity.Auth;
using Microsoft.AspNetCore.Authentication;

namespace ConstantLearning.Features.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
        services.Configure<IdentityOptions>(configuration.GetSection(IdentityOptions.SectionName));
        services.AddSingleton<TelegramInitDataValidator>();
        services.AddTransient<IClaimsTransformation, ContentEditorClaimsTransformation>();
        services.AddAuthentication(TelegramAuthentication.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TelegramAuthenticationHandler>(TelegramAuthentication.Scheme, _ => { });
        services.AddAuthorization(options => options.AddPolicy(TelegramAuthentication.ContentEditorPolicy,
            policy => policy.RequireAuthenticatedUser().RequireRole(TelegramAuthentication.ContentEditorRole)));
        return services;
    }
}
