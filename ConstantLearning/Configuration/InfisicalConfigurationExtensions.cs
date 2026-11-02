using Infisical.Sdk;
using Infisical.Sdk.Model;

namespace ConstantLearning.Configuration;

public static class InfisicalConfigurationExtensions
{
    public static void AddEnvVariablesAndConfigureSecrets(this IHostApplicationBuilder builder)
    {
        builder.Configuration.AddEnvironmentVariables();
        ConfigureSecrets(builder.Configuration, builder.Environment.EnvironmentName, LoadSecrets);
    }

    internal static void ConfigureSecrets(IConfigurationManager configuration, string environment,
        Func<string, string, string, string, string, string, IEnumerable<KeyValuePair<string, string?>>> load)
    {
        string[] names = ["INFISICAL_CLIENT_ID", "INFISICAL_CLIENT_SECRET", "INFISICAL_PROJECT_ID"];
        var credentials = names.Select(name => configuration[name]).ToArray();
        var required = configuration.GetValue<bool>("INFISICAL_REQUIRED");
        if (credentials.All(string.IsNullOrWhiteSpace) && !required) return;
        if (credentials.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Потрібні INFISICAL_CLIENT_ID, INFISICAL_CLIENT_SECRET і INFISICAL_PROJECT_ID.");

        var slug = configuration["INFISICAL_ENVIRONMENT"] ?? environment switch
        {
            "Production" => "production",
            "Staging" => "staging",
            _ => "dev"
        };
        Dictionary<string, string?> secrets;
        try
        {
            secrets = load(configuration["INFISICAL_SITE_URL"] ?? "https://eu.infisical.com",
                    credentials[0]!, credentials[1]!, credentials[2]!, slug,
                    configuration["INFISICAL_SECRET_PATH"] ?? "/app")
                .ToDictionary(pair => pair.Key.Replace("__", ":"), pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception error)
        {
            // SDK може додати credentials або відповідь сервера до винятку.
            throw new InvalidOperationException($"Не вдалося завантажити конфігурацію Infisical ({SafeFailure(error)}); перевірте machine identity, project, environment і path.");
        }

        if (required)
        {
            string[] keys = ["ConnectionStrings:Constant_Learning", "Telegram:BotToken", "Telegram:SecretToken",
                "Telegram:WebhookUrl", "Telegram:MiniAppUrl"];
            if (keys.Any(key => !secrets.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)))
                throw new InvalidOperationException("У Infisical відсутня обов’язкова конфігурація БД або Telegram.");

            var host = configuration["DEPLOY_PUBLIC_HOST"];
            if (!string.IsNullOrWhiteSpace(host)
                && (secrets["Telegram:WebhookUrl"] != $"https://{host}/api/telegram/webhook"
                    || secrets["Telegram:MiniAppUrl"] != $"https://{host}/"))
                throw new InvalidOperationException("Telegram URL у Infisical мають збігатися з PUBLIC_HOST деплою.");
        }
        configuration.AddInMemoryCollection(secrets);
    }

    private static IEnumerable<KeyValuePair<string, string?>> LoadSecrets(string host, string clientId,
        string clientSecret, string projectId, string environment, string path)
    {
        var client = new InfisicalClient(new InfisicalSdkSettingsBuilder().WithHostUri(host).Build());
        try
        {
            client.Auth().UniversalAuth().LoginAsync(clientId, clientSecret).GetAwaiter().GetResult();
        }
        catch (Exception error)
        {
            error.Data["InfisicalStage"] = "Universal Auth";
            throw;
        }
        try
        {
            var secrets = client.Secrets().ListAsync(new ListSecretsOptions
            {
                ProjectId = projectId, EnvironmentSlug = environment, SecretPath = path,
                Recursive = true, ExpandSecretReferences = true
            }).GetAwaiter().GetResult();
            return secrets.Select(secret => new KeyValuePair<string, string?>(secret.SecretKey, secret.SecretValue));
        }
        catch (Exception error)
        {
            error.Data["InfisicalStage"] = "List secrets";
            throw;
        }
    }

    private static string SafeFailure(Exception error)
    {
        var stage = error.Data["InfisicalStage"] as string;
        if (stage is not ("Universal Auth" or "List secrets")) stage = "Configuration";
        // Дозволені лише відомі назви статусів, без повідомлення або тіла відповіді SDK.
        for (Exception? cause = error; cause is not null; cause = cause.InnerException)
        {
            if (cause is HttpRequestException { StatusCode: { } code })
                return $"{stage}; HTTP {(int)code}";
            if (cause is HttpRequestException)
                foreach (var status in Enum.GetValues<System.Net.HttpStatusCode>())
                    if (cause.Message.StartsWith($"Unexpected response: {status} ", StringComparison.Ordinal))
                        return $"{stage}; HTTP {(int)status}";
        }
        var root = error.GetBaseException();
        return $"{stage}; {root.GetType().Name}";
    }
}
