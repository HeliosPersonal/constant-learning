using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Sessions.Start;
using NodaTime;

namespace ConstantLearning.Features.Learning.Sessions.Current;

/// <summary>Спільне просування до наступної доступної картки без зарахування показу чи відповіді.</summary>
public static class SessionViewBuilder
{
    /// <summary>
    /// Викликати лише після перевірки межі доби під блокуванням learner. Пропускає pending-картки
    /// вилученого зі схваленого курсу вмісту; завершення та skipped-стани зберігає викликувач.
    /// DTO не містить прийнятих відповідей чи польської леми для вправи пригадування.
    /// </summary>
    public static SessionItemResponse? PrepareNextItem(LearningSession session, IReadOnlyList<SessionItem> items,
        IReadOnlyDictionary<Guid, PublishedCourseWord> published, Instant now)
    {
        if (session.Status != "active") return null;

        foreach (var item in items.Where(item => item.State == "pending" && !published.ContainsKey(item.EntryId)))
        {
            item.State = "skipped";
        }

        var next = items.Where(item => item.State == "pending").OrderBy(item => item.Position).FirstOrDefault();
        if (next is not null) return SessionItemResponse.From(next, published[next.EntryId]);

        session.Status = "completed";
        session.CompletedAt = now;
        return null;
    }
}
