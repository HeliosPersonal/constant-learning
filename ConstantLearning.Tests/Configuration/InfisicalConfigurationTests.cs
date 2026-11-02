using ConstantLearning.Configuration;
using Microsoft.Extensions.Configuration;

namespace ConstantLearning.Tests.Configuration;

public sealed class InfisicalConfigurationTests
{
    private static ConfigurationManager Configuration(bool required = true)
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["INFISICAL_CLIENT_ID"] = "test-id", ["INFISICAL_CLIENT_SECRET"] = "test-secret",
            ["INFISICAL_PROJECT_ID"] = "test-project", ["INFISICAL_REQUIRED"] = required.ToString(),
            ["DEPLOY_PUBLIC_HOST"] = "test.example.org"
        });
        return config;
    }

    private static Dictionary<string, string?> Secrets() => new()
    {
        ["CONNECTIONSTRINGS__CONSTANT_LEARNING"] = "Host=test;Database=constant_learning",
        ["TELEGRAM__BOTTOKEN"] = "test-token", ["TELEGRAM__SECRETTOKEN"] = "test-webhook-secret",
        ["TELEGRAM__WEBHOOKURL"] = "https://test.example.org/api/telegram/webhook",
        ["TELEGRAM__MINIAPPURL"] = "https://test.example.org/"
    };

    [Theory]
    [InlineData("Production", "production")]
    [InlineData("Staging", "staging")]
    [InlineData("Development", "dev")]
    public void LoadsMappedKeysAndHeliosDefaults(string environment, string slug)
    {
        var config = Configuration();
        config["Telegram:BotToken"] = "old-token";
        InfisicalConfigurationExtensions.ConfigureSecrets(config, environment, (host, id, secret, project, actualSlug, path) =>
        {
            Assert.Equal("https://eu.infisical.com", host);
            Assert.Equal("/app", path);
            Assert.Equal(slug, actualSlug);
            Assert.Equal("test-project", project);
            return Secrets();
        });
        Assert.Equal("test-token", config["Telegram:BotToken"]);
        Assert.Equal("Host=test;Database=constant_learning", config.GetConnectionString("Constant_Learning"));
    }

    [Fact]
    public void LocalConfigurationWithoutCredentialsDoesNotContactInfisical()
    {
        var config = new ConfigurationManager();
        config["ConnectionStrings:DefaultConnection"] = "local-only";
        InfisicalConfigurationExtensions.ConfigureSecrets(config, "Development", (_, _, _, _, _, _) =>
            throw new Exception("Не повинно викликатися."));
        Assert.Equal("local-only", config.GetConnectionString("DefaultConnection"));
    }

    [Fact]
    public void PartialCredentialsFailWithoutContactingInfisical()
    {
        var config = Configuration(false);
        config["INFISICAL_CLIENT_SECRET"] = "";
        Assert.Throws<InvalidOperationException>(() => InfisicalConfigurationExtensions.ConfigureSecrets(config,
            "Production", (_, _, _, _, _, _) => throw new Exception("Не повинно викликатися.")));
    }

    [Fact]
    public void KubernetesRequiresCredentials()
    {
        var config = new ConfigurationManager();
        config["INFISICAL_REQUIRED"] = "true";
        Assert.Throws<InvalidOperationException>(() => InfisicalConfigurationExtensions.ConfigureSecrets(config,
            "Production", (_, _, _, _, _, _) => []));
    }

    [Fact]
    public void MissingRemoteSecretsCannotFallBackToLocalValues()
    {
        var config = Configuration();
        config["Telegram:BotToken"] = "local-token";
        Assert.Throws<InvalidOperationException>(() => InfisicalConfigurationExtensions.ConfigureSecrets(config,
            "Production", (_, _, _, _, _, _) => []));
    }

    [Fact]
    public void WrongTelegramHostPreventsStartup()
    {
        var secrets = Secrets();
        secrets["TELEGRAM__WEBHOOKURL"] = "https://another.example.org/api/telegram/webhook";
        Assert.Throws<InvalidOperationException>(() => InfisicalConfigurationExtensions.ConfigureSecrets(Configuration(),
            "Production", (_, _, _, _, _, _) => secrets));
    }

    [Fact]
    public void SdkErrorsDoNotExposeCredentials()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            InfisicalConfigurationExtensions.ConfigureSecrets(Configuration(), "Production",
                (_, _, _, _, _, _) => throw new Exception("test-secret confidential-response")));
        Assert.DoesNotContain("test-secret", error.ToString());
        Assert.DoesNotContain("confidential-response", error.ToString());
        Assert.Null(error.InnerException);
    }
}
