using NodaTime;

namespace ConstantLearning.Features.Learning.Domain.LearnerDay;

/// <summary>Результат перевірки IANA-поясу; запасний пояс не є підтвердженням для нагадувань.</summary>
public sealed record LearnerTimeZone(DateTimeZone Zone, bool UsedFallback);
