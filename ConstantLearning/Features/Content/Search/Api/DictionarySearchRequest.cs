using System.Globalization;
using System.Text;
using ConstantLearning.Features.Content.Search.Forms;

namespace ConstantLearning.Features.Content.Search.Api;

public sealed record DictionarySearchRequest(string FoldedQuery, bool Prefix, Guid? After, Guid? Before, int Limit)
{
    public static DictionaryError? Read(IQueryCollection query, out DictionarySearchRequest? request)
    {
        request = null;
        if (query.Any(pair => pair.Value.Count != 1 || pair.Key is not ("q" or "after" or "before" or "limit")))
            return Invalid();
        var text = query["q"].ToString();
        if (string.IsNullOrWhiteSpace(text))
            return new("dictionary.query_empty", "Введіть слово для пошуку.");
        if (text.EnumerateRunes().Count() > 200) return Invalid();
        var folded = SearchTextNormalizer.Fold(text);
        if (folded.Length == 0) return new("dictionary.query_empty", "Введіть слово для пошуку.");
        Guid? after = null, before = null;
        if (query.ContainsKey("after"))
        {
            if (!Guid.TryParseExact(query["after"], "D", out var id)) return Invalid();
            after = id;
        }
        if (query.ContainsKey("before"))
        {
            if (!Guid.TryParseExact(query["before"], "D", out var id)) return Invalid();
            before = id;
        }
        if (after is not null && before is not null) return Invalid();
        var limit = 20;
        if (query.ContainsKey("limit") && (!int.TryParse(query["limit"], NumberStyles.None,
                CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 50)) return Invalid();
        request = new(folded, folded.EnumerateRunes().Count() >= 2, after, before, limit);
        return null;
    }

    public static DictionaryError Invalid() => new("dictionary.invalid_request", "Перевірте параметри пошуку.");
}
