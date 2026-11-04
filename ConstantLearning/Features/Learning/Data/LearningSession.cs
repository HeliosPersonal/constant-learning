using NodaTime;

namespace ConstantLearning.Features.Learning.Data;

public sealed class LearningSession
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? ResultItemId { get; set; }
    public Guid LearnerId { get; set; }

    // Денормалізований дискримінатор мови (ADR-0001): session належить одній мові й не досяжна
    // через entry (session не посилається на entry).
    // TODO(T13): зняти інтеримний дефолт 'pl', коли scoping ендпоінтів писатиме мову з активного контексту.
    public string TargetLanguage { get; set; } = "pl";
    public LocalDate LearnerDay { get; set; }
    public Instant EndsAt { get; set; }
    public required string Status { get; set; }
    // unlimited-learning: plan / extra_word / extra_practice (ADR-0001). Записують усі місця створення
    // session; колонку зроблено NOT NULL контрактною міграцією після expand+backfill.
    public required string Kind { get; set; }
    public Instant? LastActivityAt { get; set; }
    public Instant? CompletedAt { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
}
