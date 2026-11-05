namespace ConstantLearning.Features.Content.Course;

/// <summary>Поточний схвалений вміст base course без доступу до внутрішніх сервісів Content.</summary>
public interface IPublishedCourseReader
{
    /// <summary>Усі мови разом — для глобального пошуку за <c>EntryId</c> (lookup) і прогріву кешу.</summary>
    Task<IReadOnlyList<PublishedCourseWord>> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Base course однієї мови у власному порядку <c>course_position</c> (T06, AC-06): добір нових
    /// лем бере тільки записи цієї <paramref name="targetLanguage"/>, мова B не протікає в добір мови A.
    /// </summary>
    Task<IReadOnlyList<PublishedCourseWord>> ReadAsync(string targetLanguage, CancellationToken cancellationToken = default);
}

public sealed record PublishedCourseWord(Guid EntryId, Guid VersionId, string Domain, int CoursePosition,
    int? Rank, string LemmaPl, string PromptUk, IReadOnlyList<string> TranslationsUk,
    string PronunciationUk, string ExamplePl, string ExampleUk, string? Gender, bool ShowContextBefore);
