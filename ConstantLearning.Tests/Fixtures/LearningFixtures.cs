using NodaTime;

namespace ConstantLearning.Tests.Fixtures;

public sealed record SessionItemData(Guid Id, Guid SessionId, Guid EntryId, Guid VersionId, int Position,
    string Kind, string State);
public sealed record LearningSessionData(Guid Id, Guid LearnerId, LocalDate LearnerDay, Instant EndsAt,
    string Status, IReadOnlyList<SessionItemData> Items);

public static class LearningFixtures
{
    public static LearningSessionData NewActiveSession(LearnerData learner,
        IEnumerable<PublishedEntryData>? items = null, IClock? clock = null)
    {
        var now = clock?.GetCurrentInstant() ?? LearnerFixtures.DefaultInstant;
        var zone = DateTimeZoneProviders.Tzdb[learner.TimeZone];
        var day = now.InZone(zone).Date;
        var sessionId = Guid.CreateVersion7(now.ToDateTimeOffset());
        var sessionItems = (items ?? []).Select((entry, index) => new SessionItemData(
            Guid.CreateVersion7(now.ToDateTimeOffset()), sessionId, entry.Id, entry.PublishedVersionId,
            index + 1, "review", "pending")).ToArray();

        return new LearningSessionData(sessionId, learner.Id, day, zone.AtStartOfDay(day.PlusDays(1)).ToInstant(),
            "active", Array.AsReadOnly(sessionItems));
    }
}
