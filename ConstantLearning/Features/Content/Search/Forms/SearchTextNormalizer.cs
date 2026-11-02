using System.Globalization;
using System.Text;

namespace ConstantLearning.Features.Content.Search.Forms;

public static class SearchTextNormalizer
{
    public static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();

    /// <summary>Спільне згортання для запису форм і пошукового запиту; не змінює навчальні відповіді.</summary>
    public static string Fold(string value)
    {
        var result = new StringBuilder();
        foreach (var character in Normalize(value).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            result.Append(character switch { 'ł' => 'l', 'ґ' => 'г', _ => character });
        }
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
