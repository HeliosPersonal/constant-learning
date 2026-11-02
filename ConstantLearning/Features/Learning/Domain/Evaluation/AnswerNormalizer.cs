using System.Text;

namespace ConstantLearning.Features.Learning.Domain.Evaluation;

public static class AnswerNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var result = new StringBuilder(normalized.Length);
        var pendingSpace = false;

        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(character switch
            {
                'а' => 'a',
                'е' => 'e',
                'і' => 'i',
                'о' => 'o',
                'р' => 'p',
                'с' => 'c',
                'у' => 'y',
                'х' => 'x',
                _ => character
            });
        }

        while (result.Length > 0 && (result[^1] is '.' or ',' or '!' or '?' or ';' or ':' or '…' or ' '))
        {
            result.Length--;
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string FoldPolishDiacritics(string normalizedValue)
    {
        ArgumentNullException.ThrowIfNull(normalizedValue);
        var result = new StringBuilder(normalizedValue.Length);

        foreach (var character in normalizedValue)
        {
            result.Append(character switch
            {
                'ą' => 'a',
                'ć' => 'c',
                'ę' => 'e',
                'ł' => 'l',
                'ń' => 'n',
                'ó' => 'o',
                'ś' => 's',
                'ź' or 'ż' => 'z',
                _ => character
            });
        }

        return result.ToString();
    }

    public static bool ContainsCyrillicLetters(string normalizedValue)
    {
        ArgumentNullException.ThrowIfNull(normalizedValue);

        foreach (var rune in normalizedValue.EnumerateRunes())
        {
            // Діапазони охоплюють кирилицю, її розширення та фонетичні кириличні літери.
            var isCyrillic = rune.Value is >= 0x0400 and <= 0x052F
                or >= 0x1C80 and <= 0x1C8F
                or 0x1D2B or 0x1D78
                or >= 0x2DE0 and <= 0x2DFF
                or >= 0xA640 and <= 0xA69F
                or >= 0x1E030 and <= 0x1E08F;

            if (isCyrillic && Rune.IsLetter(rune))
            {
                return true;
            }
        }

        return false;
    }
}
