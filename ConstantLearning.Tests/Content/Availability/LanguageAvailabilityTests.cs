using ConstantLearning.Features.Content.Availability;
using AwesomeAssertions;

namespace ConstantLearning.Tests.Content.Availability;

/// <summary>
/// T06 DoD: чиста логіка прапорця <c>available</c> для всіх комбінацій (enabled/disabled) × (є/нема
/// контенту). Мова доступна лише коли ввімкнена І має схвалений контент (AC-02, ADR-0004).
/// </summary>
public sealed class LanguageAvailabilityTests
{
    [Theory]
    [InlineData(true, true, true)]    // ввімкнена + контент → доступна
    [InlineData(true, false, false)]  // ввімкнена, але без контенту → недоступна (AC-02)
    [InlineData(false, true, false)]  // контент є, але вимкнена → недоступна
    [InlineData(false, false, false)] // вимкнена без контенту → недоступна
    public void IsAvailable_RequiresBothEnabledAndApprovedContent(bool isEnabled, bool hasApprovedContent, bool expected) =>
        LanguageAvailability.IsAvailable(isEnabled, hasApprovedContent).Should().Be(expected);
}
