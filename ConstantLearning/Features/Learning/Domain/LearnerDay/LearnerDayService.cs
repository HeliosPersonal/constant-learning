using NodaTime;

namespace ConstantLearning.Features.Learning.Domain.LearnerDay;

public sealed class LearnerDayService(IClock clock) : ILearnerDayService
{
    public const string FallbackTimeZoneId = "Europe/Warsaw";

    public LearnerTimeZone ResolveTimeZone(string? timeZoneId)
    {
        var zone = timeZoneId is null ? null : DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId);
        return new LearnerTimeZone(zone ?? DateTimeZoneProviders.Tzdb[FallbackTimeZoneId], zone is null);
    }

    public LearnerDayPeriod GetCurrentDay(string? timeZoneId, LearnerDayPeriod? previousDay = null)
    {
        var now = clock.GetCurrentInstant();
        var zone = ResolveTimeZone(timeZoneId).Zone;
        Instant? previousEnd = null;

        if (previousDay is not null)
        {
            if (now < previousDay.StartsAt)
            {
                throw new ArgumentException("Збережена доба ще не почалася за поточним годинником.", nameof(previousDay));
            }

            // До початкової межі останній вибраний пояс може скасувати майбутнє подовження.
            // Уже розпочате подовження не скорочується: доба не завершується заднім числом.
            var end = now < previousDay.OriginalEndsAt ? previousDay.OriginalEndsAt : previousDay.EndsAt;
            if (end.InZone(zone).Date <= previousDay.Date)
            {
                // Не повторюємо дату навіть при перетині міжнародної лінії зміни дат на захід.
                end = StartOfNextExistingDay(previousDay.Date, zone);
            }

            if (now < end)
            {
                return new LearnerDayPeriod(previousDay.Date, previousDay.StartsAt, end,
                    previousDay.TimeZoneAtStart, previousDay.OriginalEndsAt);
            }

            previousEnd = end;
        }

        var date = now.InZone(zone).Date;
        var start = date.AtStartOfDayInZone(zone).ToInstant();
        if (previousEnd is { } boundary && boundary > start)
        {
            start = boundary;
        }

        return new LearnerDayPeriod(date, start, StartOfNextExistingDay(date, zone), zone.Id);
    }

    private static Instant StartOfNextExistingDay(LocalDate date, DateTimeZone zone)
    {
        while (true)
        {
            date = date.PlusDays(1);
            try
            {
                return date.AtStartOfDayInZone(zone).ToInstant();
            }
            catch (SkippedTimeException)
            {
                // Зміна державного поясу може пропустити цілу дату, наприклад у Pacific/Apia.
            }
        }
    }
}
