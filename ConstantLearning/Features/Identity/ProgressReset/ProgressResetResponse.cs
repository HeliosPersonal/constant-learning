using System.Text.Json.Serialization;

namespace ConstantLearning.Features.Identity.ProgressReset;

/// <summary>Відповідь скидання прогресу (контракт <c>ProgressResetResult</c>).</summary>
public sealed record ProgressResetResponse([property: JsonPropertyName("erased")] bool Erased);
