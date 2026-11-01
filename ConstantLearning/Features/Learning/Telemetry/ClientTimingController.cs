using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConstantLearning.Platform.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstantLearning.Features.Learning.Telemetry;

[ApiController]
[Authorize]
[Route("api/v1/telemetry/client-timings")]
public sealed class ClientTimingController(TrainerMetrics metrics) : ControllerBase
{
    [HttpPost(Name = "reportClientTiming")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<TelemetryError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReportClientTiming(CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
            if (!TryReadTiming(document.RootElement, out var milliseconds))
            {
                return InvalidMetric();
            }

            metrics.RecordTodayReady(milliseconds);
            return NoContent();
        }
        catch (JsonException)
        {
            return InvalidMetric();
        }
    }

    private static bool TryReadTiming(JsonElement root, out int milliseconds)
    {
        milliseconds = 0;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var properties = root.EnumerateObject().ToArray();
        if (properties.Length != 2
            || !root.TryGetProperty("metric", out var metric)
            || metric.ValueKind != JsonValueKind.String
            || metric.GetString() != "today_ready_ms"
            || !root.TryGetProperty("value_ms", out var value)
            || value.ValueKind != JsonValueKind.Number
            || !TryReadMilliseconds(value.GetRawText(), out milliseconds))
        {
            return false;
        }

        return true;
    }

    private static bool TryReadMilliseconds(string raw, out int milliseconds)
    {
        milliseconds = 0;
        var exponentStart = raw.IndexOfAny(['e', 'E']);
        var mantissa = exponentStart < 0 ? raw : raw[..exponentStart];
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal).TrimStart('-').TrimStart('0');
        if (digits.Length == 0)
        {
            return true;
        }

        var exponent = 0;
        if (raw[0] == '-'
            || (exponentStart >= 0 && !int.TryParse(raw[(exponentStart + 1)..], NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out exponent)))
        {
            return false;
        }

        var decimalPoint = mantissa.IndexOf('.');
        var fractionalDigits = decimalPoint < 0 ? 0 : mantissa.Length - decimalPoint - 1;
        var significantDigits = digits.TrimEnd('0');
        // JSON Schema вважає 1.0 та 1e2 цілими; перевіряємо цифри без округлення дробової частини.
        var scale = (long)exponent - fractionalDigits + digits.Length - significantDigits.Length;
        if (scale < 0 || significantDigits.Length + scale > 5)
        {
            return false;
        }

        milliseconds = int.Parse(significantDigits, CultureInfo.InvariantCulture);
        for (var power = 0; power < scale; power++)
        {
            milliseconds *= 10;
        }

        return milliseconds <= 60000;
    }

    private BadRequestObjectResult InvalidMetric() => BadRequest(
        new TelemetryError("telemetry.invalid_metric", "Невідома метрика або некоректне значення."));
}

public sealed record TelemetryError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);
