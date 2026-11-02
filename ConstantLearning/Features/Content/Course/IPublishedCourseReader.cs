namespace ConstantLearning.Features.Content.Course;

/// <summary>Поточний схвалений вміст base course без доступу до внутрішніх сервісів Content.</summary>
public interface IPublishedCourseReader
{
    Task<IReadOnlyList<PublishedCourseWord>> ReadAsync(CancellationToken cancellationToken = default);
}

public sealed record PublishedCourseWord(Guid EntryId, Guid VersionId, string Domain, int CoursePosition,
    int? Rank, string LemmaPl, string PromptUk, IReadOnlyList<string> TranslationsUk,
    string PronunciationUk, string ExamplePl, string ExampleUk, string? Gender, bool ShowContextBefore);
