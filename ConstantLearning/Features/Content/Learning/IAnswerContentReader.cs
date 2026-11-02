using ConstantLearning.Features.Content.Course;

namespace ConstantLearning.Features.Content.Learning;

/// <summary>Схвалений вміст для оцінювання, синонімів та наступної картки без доступу до таблиць Content.</summary>
public interface IAnswerContentReader
{
    Task<AnswerContentSnapshot> ReadAsync(Guid entryId, CancellationToken cancellationToken = default);
}

public sealed record AnswerContentSnapshot(IReadOnlyList<PublishedCourseWord> Course,
    PublishedCourseWord? Target, IReadOnlyList<string> AcceptedAnswers);
