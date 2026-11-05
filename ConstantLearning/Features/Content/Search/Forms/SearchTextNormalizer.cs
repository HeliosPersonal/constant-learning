using System.Globalization;
using System.Text;

namespace ConstantLearning.Features.Content.Search.Forms;

public static class SearchTextNormalizer
{
    public static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();

    /// <summary>
    /// Спільне, мово-незалежне згортання для запису форм і пошукового запиту; не змінює навчальні відповіді.
    /// Спершу застосовує об'єднані підстановки всіх профілів (<see cref="NormalizationProfiles.FoldSubstitutions"/>)
    /// — суперсет мов, — а тоді загальне зняття діакритики (NFD). Підстановки йдуть <b>до</b> NFD навмисно:
    /// німецьке ä→ae/ß→ss має впіймати прекомпозований символ, перш ніж NFD зніме його діакритику (інакше ä
    /// розклалося б на a+комбінаційний знак і згорнулося у «a»). Для підстановок без NFD-декомпозиції
    /// (pl ł→l, uk ґ→г) порядок байт-ідентичний. Згортання однакове для запиту й форм, доки пошук не scoped
    /// за активною мовою (до T13).
    /// </summary>
    public static string Fold(string value)
    {
        var substituted = new StringBuilder();
        foreach (var character in Normalize(value))
            substituted.Append(NormalizationProfiles.FoldSubstitutions.GetValueOrDefault(character, char.ToString(character)));
        var result = new StringBuilder();
        foreach (var character in substituted.ToString().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            result.Append(character);
        }
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
