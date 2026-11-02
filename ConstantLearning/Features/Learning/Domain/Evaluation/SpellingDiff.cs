using System.Globalization;

namespace ConstantLearning.Features.Learning.Domain.Evaluation;

public static class SpellingDiff
{
    /// <summary>Повертає мінімальні редагування графем; пріоритет за рівної вартості: заміна, вставка, видалення.</summary>
    public static IReadOnlyList<DiffSegment> Create(string expected, string actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        var expectedElements = GetTextElements(expected);
        var actualElements = GetTextElements(actual);
        var distances = new int[expectedElements.Length + 1, actualElements.Length + 1];

        for (var i = expectedElements.Length; i >= 0; i--)
        {
            for (var j = actualElements.Length; j >= 0; j--)
            {
                if (i == expectedElements.Length)
                {
                    distances[i, j] = actualElements.Length - j;
                }
                else if (j == actualElements.Length)
                {
                    distances[i, j] = expectedElements.Length - i;
                }
                else if (expectedElements[i] == actualElements[j])
                {
                    distances[i, j] = distances[i + 1, j + 1];
                }
                else
                {
                    distances[i, j] = 1 + Math.Min(distances[i + 1, j + 1],
                        Math.Min(distances[i + 1, j], distances[i, j + 1]));
                }
            }
        }

        var segments = new List<DiffSegment>();
        var expectedIndex = 0;
        var actualIndex = 0;

        while (expectedIndex < expectedElements.Length || actualIndex < actualElements.Length)
        {
            DiffSegment segment;

            if (expectedIndex < expectedElements.Length && actualIndex < actualElements.Length
                && expectedElements[expectedIndex] == actualElements[actualIndex])
            {
                segment = new(DiffOperation.Equal, expectedElements[expectedIndex++], actualElements[actualIndex++]);
            }
            else if (expectedIndex < expectedElements.Length && actualIndex < actualElements.Length
                && distances[expectedIndex, actualIndex] == 1 + distances[expectedIndex + 1, actualIndex + 1])
            {
                segment = new(DiffOperation.Substitute, expectedElements[expectedIndex++], actualElements[actualIndex++]);
            }
            else if (expectedIndex < expectedElements.Length
                && distances[expectedIndex, actualIndex] == 1 + distances[expectedIndex + 1, actualIndex])
            {
                segment = new(DiffOperation.Insert, expectedElements[expectedIndex++], string.Empty);
            }
            else
            {
                segment = new(DiffOperation.Delete, string.Empty, actualElements[actualIndex++]);
            }

            if (segments.Count > 0 && segments[^1].Op == segment.Op)
            {
                var previous = segments[^1];
                segments[^1] = previous with
                {
                    Expected = previous.Expected + segment.Expected,
                    Actual = previous.Actual + segment.Actual
                };
            }
            else
            {
                segments.Add(segment);
            }
        }

        return segments.AsReadOnly();
    }

    private static string[] GetTextElements(string value)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(value);

        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }

        return elements.ToArray();
    }
}
