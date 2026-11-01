using ConstantLearning.Features.Bot;
using FluentAssertions;
using NetArchTest.Rules;

namespace ConstantLearning.Tests.Architecture;

public class ModuleBoundaryTests
{
    private const string FeatureNamespace = "ConstantLearning.Features.";

    [Theory]
    [InlineData("Identity")]
    [InlineData("Learning")]
    [InlineData("Content")]
    [InlineData("Notifications")]
    [InlineData("Bot")]
    public void FeatureModule_DoesNotDependOnOtherModulesInternalTypes(string module)
    {
        var assembly = typeof(BotModule).Assembly;
        var moduleNamespace = FeatureNamespace + module;
        var forbiddenTypes = assembly.GetTypes()
            .Where(type => type.Namespace is not null
                           && type.Namespace.StartsWith(FeatureNamespace, StringComparison.Ordinal)
                           && type.Namespace != moduleNamespace
                           && !type.Namespace.StartsWith(moduleNamespace + ".", StringComparison.Ordinal)
                           && !type.IsVisible)
            .Select(type => type.FullName!.Replace('+', '/'))
            .ToArray();

        if (forbiddenTypes.Length == 0)
        {
            return;
        }

        var result = Types.InAssembly(assembly)
            .That().ResideInNamespace(moduleNamespace)
            .ShouldNot().HaveDependencyOnAny(forbiddenTypes)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "модуль {0} не повинен залежати від внутрішніх типів інших модулів; порушники: {1}",
            module, string.Join(", ", result.FailingTypes?.Select(type => type.FullName) ?? []));
    }
}
