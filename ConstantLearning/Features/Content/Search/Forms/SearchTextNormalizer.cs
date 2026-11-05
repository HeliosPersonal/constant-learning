using System.Globalization;
using System.Text;

namespace ConstantLearning.Features.Content.Search.Forms;

public static class SearchTextNormalizer
{
    public static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();

    /// <summary>
    /// Спільне, мово-незалежне згортання для запису форм і пошукового запиту; не змінює навчальні відповіді.
    /// Понад загальне зняття діакритики (NFD) застосовує об'єднані підстановки всіх профілів
    /// (<see cref="NormalizationProfiles.FoldSubstitutions"/>) — суперсет мов, щоб запит і форми згорталися
    /// однаково, доки пошук не scoped за активною мовою (до T13).
    /// </summary>
    public static string Fold(string value)
    {
        var result = new StringBuilder();
        foreach (var character in Normalize(value).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            result.Append(NormalizationProfiles.FoldSubstitutions.GetValueOrDefault(character, character));
        }
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
