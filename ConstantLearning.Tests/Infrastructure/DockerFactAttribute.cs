using DotNet.Testcontainers.Configurations;

namespace ConstantLearning.Tests.Infrastructure;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        Skip = DockerAvailability.GetSkipReason(DockerAvailability.IsCi, DockerAvailability.UnavailableReason);
    }
}

internal static class DockerAvailability
{
    private static readonly Lazy<string?> ProbeResult = new(Probe);

    public static bool IsCi => IsEnabled(Environment.GetEnvironmentVariable("CI"))
                               || IsEnabled(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"))
                               || IsEnabled(Environment.GetEnvironmentVariable("TF_BUILD"));

    public static string? UnavailableReason => ProbeResult.Value;

    public static string? GetSkipReason(bool isCi, string? unavailableReason) =>
        isCi ? null : unavailableReason;

    public static void RequireDocker(bool isCi, string? unavailableReason)
    {
        if (isCi && unavailableReason is not null)
        {
            throw new InvalidOperationException($"Інтеграційні тести обов’язкові в CI. {unavailableReason}");
        }
    }

    private static bool IsEnabled(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);

    private static string? Probe()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var client = TestcontainersSettings.OS.DockerEndpointAuthConfig
                .GetDockerClientBuilder(Guid.NewGuid()).Build();
            client.System.PingAsync(timeout.Token).GetAwaiter().GetResult();
            return null;
        }
        catch (Exception exception)
        {
            return $"Docker недоступний ({exception.GetType().Name}). Запустіть Docker і повторіть dotnet test.";
        }
    }
}
