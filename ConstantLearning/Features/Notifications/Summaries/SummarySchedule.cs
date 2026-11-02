using ConstantLearning.Features.Identity.NotificationAudience;
using NodaTime;

namespace ConstantLearning.Features.Notifications.Summaries;

internal static class SummarySchedule
{
    public static Instant At(LocalDate date, string timeZone, int hour) =>
        DateTimeZoneProviders.Tzdb[timeZone].AtLeniently(date.At(new LocalTime(hour, 0))).ToInstant();

    public static Instant? CurrentLearnerDue(LearnerNotificationRecipient recipient)
    {
        var due = At(recipient.LocalDate, recipient.TimeZone, 12);
        return due >= recipient.DayStartsAt && due < recipient.DayEndsAt ? due : null;
    }

    public static (LocalDate Date, Instant Due) NextLearnerDue(LearnerNotificationRecipient recipient)
    {
        var zone = DateTimeZoneProviders.Tzdb[recipient.TimeZone];
        var date = recipient.DayEndsAt.InZone(zone).Date;
        var due = At(date, zone.Id, 12);
        while (due < recipient.DayEndsAt)
        {
            date = date.PlusDays(1);
            due = At(date, zone.Id, 12);
        }
        return (due.InZone(zone).Date, due);
    }
}
