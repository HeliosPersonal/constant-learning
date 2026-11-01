namespace ConstantLearning.Features.Learning.Domain.Scheduling;

/// <summary>Межі PRD суперечать одна одній; правило для фактичного інтервалу від 180 діб ще потребує рішення.</summary>
public sealed class ReviewIntervalConflictException(int previousIntervalDays)
    : InvalidOperationException($"Неможливо збільшити інтервал {previousIntervalDays} діб і водночас дотриматися межі 180 діб.")
{
    public int PreviousIntervalDays { get; } = previousIntervalDays;
}
