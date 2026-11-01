namespace ConstantLearning.Features.Learning.Domain.LearnerDay;

public interface ILearnerDayService
{
    LearnerTimeZone ResolveTimeZone(string? timeZoneId);

    /// <summary>
    /// Обчислює добу за годинником. Після зміни поясу потрібен збережений попередній знімок;
    /// без нього обчислюється звичайна календарна доба поточного поясу.
    /// Результат замінює знімок доби, але не збережений EndsAt уже створеної session.
    /// </summary>
    LearnerDayPeriod GetCurrentDay(string? timeZoneId, LearnerDayPeriod? previousDay = null);
}
