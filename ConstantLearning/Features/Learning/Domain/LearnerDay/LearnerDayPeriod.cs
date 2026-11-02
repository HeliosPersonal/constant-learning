using NodaTime;

namespace ConstantLearning.Features.Learning.Domain.LearnerDay;

/// <summary>Незмінний знімок доби, який споживач зберігає між запитами та змінами поясу.</summary>
public sealed record LearnerDayPeriod
{
    public LearnerDayPeriod(
        LocalDate date,
        Instant startsAt,
        Instant endsAt,
        string timeZoneAtStart,
        Instant? originalEndsAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneAtStart);
        var originalEnd = originalEndsAt ?? endsAt;
        if (startsAt >= originalEnd || originalEnd > endsAt)
        {
            throw new ArgumentException("Межі доби мають задовольняти StartsAt < OriginalEndsAt <= EndsAt.");
        }

        Date = date;
        StartsAt = startsAt;
        EndsAt = endsAt;
        TimeZoneAtStart = timeZoneAtStart;
        OriginalEndsAt = originalEnd;
    }

    /// <summary>Дата для денних лімітів; не змінюється всередині подовженої доби.</summary>
    public LocalDate Date { get; }

    /// <summary>Включна UTC-межа; після зміни поясу може бути пізнішою за місцеву опівніч.</summary>
    public Instant StartsAt { get; }

    /// <summary>Виключна UTC-межа з урахуванням подовження після зміни поясу.</summary>
    public Instant EndsAt { get; }

    /// <summary>Незмінна межа за поясом початку, до будь-яких подовжень.</summary>
    public Instant OriginalEndsAt { get; }

    public string TimeZoneAtStart { get; }
}
