using ConstantLearning.Features.Learning.Domain.LearnerDay;
using FluentAssertions;
using NodaTime;
using NodaTime.Testing;

namespace ConstantLearning.Tests.Learning.Domain.LearnerDay;

public class LearnerDayServiceTests
{
    [Fact]
    public void Ac07_WarsawToTokyo_KeepsQuotaUntilWarsawMidnightThenStartsShortDay()
    {
        var clock = new FakeClock(Instant.FromUtc(2026, 1, 15, 11, 0));
        var service = new LearnerDayService(clock);
        var issuedDay = service.GetCurrentDay("Europe/Warsaw");
        clock.Advance(Duration.FromHours(8));

        var changedDay = service.GetCurrentDay("Asia/Tokyo", issuedDay);

        changedDay.Date.Should().Be(new LocalDate(2026, 1, 15));
        changedDay.EndsAt.Should().Be(Instant.FromUtc(2026, 1, 15, 23, 0));
        changedDay.EndsAt.InZone(DateTimeZoneProviders.Tzdb["Asia/Tokyo"]).TimeOfDay
            .Should().Be(new LocalTime(8, 0));
        clock.Reset(changedDay.EndsAt - Duration.FromNanoseconds(1));
        service.GetCurrentDay("Asia/Tokyo", changedDay).Should().Be(changedDay);

        clock.Reset(changedDay.EndsAt);
        var nextDay = service.GetCurrentDay("Asia/Tokyo", changedDay);

        nextDay.Date.Should().Be(new LocalDate(2026, 1, 16));
        nextDay.StartsAt.Should().Be(changedDay.EndsAt);
        nextDay.EndsAt.Should().Be(Instant.FromUtc(2026, 1, 16, 15, 0));
        (nextDay.EndsAt - nextDay.StartsAt).Should().Be(Duration.FromHours(16));
        nextDay.TimeZoneAtStart.Should().Be("Asia/Tokyo");

        clock.Reset(nextDay.EndsAt);
        var followingDay = service.GetCurrentDay("Asia/Tokyo", nextDay);
        followingDay.Date.Should().Be(new LocalDate(2026, 1, 17));
        followingDay.StartsAt.Should().Be(nextDay.EndsAt);
        (followingDay.EndsAt - followingDay.StartsAt).Should().Be(Duration.FromHours(24));
    }

    [Fact]
    public void Ac54_WarsawToNewYork_ExtendsQuotaDayButPreservesExistingSessionEnd()
    {
        var clock = new FakeClock(Instant.FromUtc(2026, 1, 15, 11, 0));
        var service = new LearnerDayService(clock);
        var issuedDay = service.GetCurrentDay("Europe/Warsaw");
        var sessionEndsAt = issuedDay.EndsAt;

        var changedDay = service.GetCurrentDay("America/New_York", issuedDay);
        clock.Reset(Instant.FromUtc(2026, 1, 16, 0, 0));
        var atSevenPm = service.GetCurrentDay("America/New_York", changedDay);

        atSevenPm.Date.Should().Be(new LocalDate(2026, 1, 15));
        atSevenPm.StartsAt.Should().Be(Instant.FromUtc(2026, 1, 14, 23, 0));
        atSevenPm.EndsAt.Should().Be(Instant.FromUtc(2026, 1, 16, 5, 0));
        atSevenPm.OriginalEndsAt.Should().Be(sessionEndsAt);
        issuedDay.EndsAt.Should().Be(Instant.FromUtc(2026, 1, 15, 23, 0));
        (atSevenPm.EndsAt - atSevenPm.StartsAt).Should().Be(Duration.FromHours(30));

        clock.Reset(atSevenPm.EndsAt - Duration.FromNanoseconds(1));
        service.GetCurrentDay("America/New_York", atSevenPm).Date.Should().Be(atSevenPm.Date);
        clock.Reset(atSevenPm.EndsAt);
        var nextDay = service.GetCurrentDay("America/New_York", atSevenPm);
        nextDay.Date.Should().Be(new LocalDate(2026, 1, 16));
        nextDay.StartsAt.Should().Be(atSevenPm.EndsAt);
        nextDay.EndsAt.Should().Be(Instant.FromUtc(2026, 1, 17, 5, 0));
    }

    [Theory]
    [InlineData(3, 29, 23, 28, 23, 29, 22)]
    [InlineData(10, 25, 25, 24, 22, 25, 23)]
    public void WarsawDst_UsesCalendarBoundariesInsteadOfTwentyFourHours(
        int month, int day, int hours, int startDay, int startHour, int endDay, int endHour)
    {
        var clock = new FakeClock(Instant.FromUtc(2026, month, day, 12, 0));
        var service = new LearnerDayService(clock);

        var period = service.GetCurrentDay("Europe/Warsaw");

        period.Date.Should().Be(new LocalDate(2026, month, day));
        period.StartsAt.Should().Be(Instant.FromUtc(2026, month, startDay, startHour, 0));
        period.EndsAt.Should().Be(Instant.FromUtc(2026, month, endDay, endHour, 0));
        (period.EndsAt - period.StartsAt).Should().Be(Duration.FromHours(hours));
        clock.Reset(period.EndsAt);
        service.GetCurrentDay("Europe/Warsaw", period).Date.Should().Be(period.Date.PlusDays(1));
    }

    [Theory]
    [InlineData(3, 29, 0, 30)]
    [InlineData(10, 25, 0, 30)]
    public void DstTransition_DoesNotChangeDayOrItsBoundaries(int month, int day, int hour, int minute)
    {
        var clock = new FakeClock(Instant.FromUtc(2026, month, day, hour, minute));
        var service = new LearnerDayService(clock);
        var period = service.GetCurrentDay("Europe/Warsaw");

        clock.Advance(Duration.FromHours(1));

        service.GetCurrentDay("Europe/Warsaw", period).Should().Be(period);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Unknown/Zone")]
    [InlineData("europe/warsaw")]
    [InlineData("Central European Standard Time")]
    public void MissingOrUnknownIanaZone_FallsBackToWarsawWithFlag(string? zoneId)
    {
        var clock = new FakeClock(Instant.FromUtc(2026, 1, 15, 23, 30));
        var service = new LearnerDayService(clock);

        var resolved = service.ResolveTimeZone(zoneId);

        resolved.Zone.Id.Should().Be("Europe/Warsaw");
        resolved.UsedFallback.Should().BeTrue();
        service.GetCurrentDay(zoneId).Should().Be(service.GetCurrentDay("Europe/Warsaw"));
        service.GetCurrentDay(zoneId).Date.Should().Be(new LocalDate(2026, 1, 16));
    }

    [Theory]
    [InlineData("Europe/Warsaw")]
    [InlineData("Asia/Tokyo")]
    [InlineData("America/New_York")]
    public void ValidIanaZone_DoesNotUseFallback(string zoneId)
    {
        var service = new LearnerDayService(new FakeClock(Instant.FromUtc(2026, 1, 15, 12, 0)));

        var resolved = service.ResolveTimeZone(zoneId);

        resolved.Zone.Id.Should().Be(zoneId);
        resolved.UsedFallback.Should().BeFalse();
    }

    [Fact]
    public void RepeatedChangesBeforeOriginalMidnight_UseLatestZoneWithoutResettingQuota()
    {
        var clock = new FakeClock(Instant.FromUtc(2026, 1, 15, 11, 0));
        var service = new LearnerDayService(clock);
        var initial = service.GetCurrentDay("Europe/Warsaw");
        var newYork = service.GetCurrentDay("America/New_York", initial);
        newYork.EndsAt.Should().Be(Instant.FromUtc(2026, 1, 16, 5, 0));

        clock.Advance(Duration.FromHours(1));
        var tokyo = service.GetCurrentDay("Asia/Tokyo", newYork);
        tokyo.Should().Be(initial);

        var newYorkAgain = service.GetCurrentDay("America/New_York", tokyo);
        newYorkAgain.Should().Be(newYork);
        service.GetCurrentDay("Europe/Warsaw", newYorkAgain).Should().Be(initial);
    }

    [Fact]
    public void RepeatedChangeDuringExtension_DoesNotRetroactivelyEndDay()
    {
        var clock = new FakeClock(Instant.FromUtc(2026, 1, 15, 11, 0));
        var service = new LearnerDayService(clock);
        var initial = service.GetCurrentDay("Europe/Warsaw");
        var newYork = service.GetCurrentDay("America/New_York", initial);
        clock.Reset(Instant.FromUtc(2026, 1, 16, 0, 0));

        var tokyo = service.GetCurrentDay("Asia/Tokyo", newYork);

        tokyo.Should().Be(newYork);
        clock.Reset(tokyo.EndsAt);
        var nextDay = service.GetCurrentDay("Asia/Tokyo", tokyo);
        nextDay.Date.Should().Be(new LocalDate(2026, 1, 16));
        nextDay.StartsAt.Should().Be(tokyo.EndsAt);
        nextDay.EndsAt.Should().Be(Instant.FromUtc(2026, 1, 16, 15, 0));
    }

    [Fact]
    public void RepeatedWestwardChangeDuringExtension_ExtendsToLatestZoneMidnight()
    {
        var clock = new FakeClock(Instant.FromUtc(2026, 1, 15, 11, 0));
        var service = new LearnerDayService(clock);
        var initial = service.GetCurrentDay("Europe/Warsaw");
        var newYork = service.GetCurrentDay("America/New_York", initial);
        clock.Reset(Instant.FromUtc(2026, 1, 16, 0, 0));

        var losAngeles = service.GetCurrentDay("America/Los_Angeles", newYork);

        losAngeles.Date.Should().Be(initial.Date);
        losAngeles.EndsAt.Should().Be(Instant.FromUtc(2026, 1, 16, 8, 0));
        losAngeles.OriginalEndsAt.Should().Be(initial.EndsAt);
    }

    [Fact]
    public void SeveralInactiveDays_SkipsDirectlyToCurrentDay()
    {
        var clock = new FakeClock(Instant.FromUtc(2026, 1, 15, 11, 0));
        var service = new LearnerDayService(clock);
        var previous = service.GetCurrentDay("Europe/Warsaw");
        clock.Advance(Duration.FromDays(5));

        var current = service.GetCurrentDay("Asia/Tokyo", previous);

        current.Should().Be(service.GetCurrentDay("Asia/Tokyo"));
        current.Date.Should().Be(new LocalDate(2026, 1, 20));
    }

    [Fact]
    public void MidnightGap_UsesFirstExistingInstantOfDay()
    {
        var service = new LearnerDayService(new FakeClock(Instant.FromUtc(2018, 11, 4, 12, 0)));

        var day = service.GetCurrentDay("America/Sao_Paulo");

        day.StartsAt.Should().Be(Instant.FromUtc(2018, 11, 4, 3, 0));
        day.EndsAt.Should().Be(Instant.FromUtc(2018, 11, 5, 2, 0));
    }

    [Fact]
    public void EntireSkippedDate_EndsAtStartOfNextExistingDate()
    {
        var clock = new FakeClock(Instant.FromUtc(2011, 12, 29, 12, 0));
        var service = new LearnerDayService(clock);

        var day = service.GetCurrentDay("Pacific/Apia");

        day.Date.Should().Be(new LocalDate(2011, 12, 29));
        day.EndsAt.Should().Be(Instant.FromUtc(2011, 12, 30, 10, 0));
        clock.Reset(day.EndsAt);
        service.GetCurrentDay("Pacific/Apia", day).Date.Should().Be(new LocalDate(2011, 12, 31));
    }

    [Fact]
    public void FutureSnapshot_IsRejected()
    {
        var clock = new FakeClock(Instant.FromUtc(2026, 1, 15, 11, 0));
        var service = new LearnerDayService(clock);
        var day = service.GetCurrentDay("Europe/Warsaw");
        clock.Reset(day.StartsAt - Duration.FromNanoseconds(1));

        var action = () => service.GetCurrentDay("Europe/Warsaw", day);

        action.Should().Throw<ArgumentException>().WithParameterName("previousDay");
    }
}
