using NodaTime;

namespace ConstantLearning.Features.Learning.Domain.Scheduling;

/// <summary>Чистий порт планування без доступу до БД, системного годинника чи глобального генератора випадковості.</summary>
public interface IReviewScheduler
{
    string Version { get; }

    ReviewState Introduce(LocalDate learnerDay, bool alreadyKnown = false);

    ReviewState Apply(ReviewState state, ReviewAnswer answer);
}
