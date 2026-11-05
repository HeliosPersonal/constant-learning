using AwesomeAssertions;

namespace ConstantLearning.Tests.Infrastructure;

public sealed class DockerAvailabilityTests
{
    [Fact]
    public void MissingDocker_LocallyProducesAnExplicitSkip()
    {
        DockerAvailability.GetSkipReason(false, "Docker недоступний.").Should().Be("Docker недоступний.");
        var require = () => DockerAvailability.RequireDocker(false, "Docker недоступний.");
        require.Should().NotThrow();
    }

    [Fact]
    public void MissingDocker_InCiFailsInsteadOfSkipping()
    {
        DockerAvailability.GetSkipReason(true, "Docker недоступний.").Should().BeNull();
        var require = () => DockerAvailability.RequireDocker(true, "Docker недоступний.");
        require.Should().Throw<InvalidOperationException>().WithMessage("*обов’язкові в CI*Docker недоступний*");
    }
}
