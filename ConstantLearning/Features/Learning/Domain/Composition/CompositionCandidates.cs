using ConstantLearning.Features.Learning.Domain.Scheduling;

namespace ConstantLearning.Features.Learning.Domain.Composition;

public enum DomainQuota
{
    Everyday,
    Legalization
}

/// <summary>
/// Знімок леми base course. Rank — frequency rank для Everyday або utility rank для Legalization.
/// IsPresented означає перший показ або «Вже знаю»; непоказана повернута картка має false.
/// Не схвалені леми не беруть участі ані у виборі, ані в частках квот.
/// </summary>
public sealed record CourseWordCandidate(
    Guid EntryId,
    DomainQuota Quota,
    int? Rank,
    int? CoursePosition,
    bool IsApproved,
    bool IsPresented);

/// <summary>Стан повторення та його місце в base course, без залежності від persistence-моделі.</summary>
public sealed record ReviewCandidate(Guid EntryId, int CoursePosition, ReviewState State);
