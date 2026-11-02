using ConstantLearning.Features.Identity.NotificationAudience;
using ConstantLearning.Features.Identity.ReminderAudience;
using NodaTime;

namespace ConstantLearning.Features.Notifications.Reminders;

internal static class ReminderSchedule
{
    public static Instant? Due(LearnerNotificationRecipient recipient, ReminderPreferences preferences)
    {
        if ((preferences.DaysOfWeek & (1 << ((int)recipient.LocalDate.DayOfWeek - 1))) == 0) return null;
        // Пропущений час зсуваємо на тривалість DST-розриву; подвійний — перше входження.
        var due = DateTimeZoneProviders.Tzdb[recipient.TimeZone].AtLeniently(recipient.LocalDate.At(preferences.LocalTime)).ToInstant();
        return due >= recipient.DayStartsAt && due < recipient.DayEndsAt ? due : null;
    }
}
