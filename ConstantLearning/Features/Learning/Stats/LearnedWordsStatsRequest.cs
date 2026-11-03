using System.Globalization;
using ConstantLearning.Features.Content.Search.Forms;

namespace ConstantLearning.Features.Learning.Stats;

/// <summary>
/// Розбір і валідація параметрів GET /api/v1/stats/words. Дзеркалить стиль DictionarySearchRequest:
/// білий список ключів, курсор after XOR before, limit 1..50. `tab` і `sort` мають дефолти.
/// </summary>
public sealed record LearnedWordsStatsRequest(string Tab, string Sort, string? FoldedQuery, Guid? After, Guid? Before, int Limit)
{
    private static readonly string[] Tabs = ["all", "recalled", "review"];
    private static readonly string[] Sorts = ["recent", "alpha", "weakest"];

    public static StatsError? Read(IQueryCollection query, out LearnedWordsStatsRequest? request)
    {
        request = null;
        if (query.Any(pair => pair.Value.Count != 1
                || pair.Key is not ("tab" or "sort" or "q" or "after" or "before" or "limit")))
            return Invalid();

        var tab = query.ContainsKey("tab") ? query["tab"].ToString() : "all";
        if (!Tabs.Contains(tab, StringComparer.Ordinal)) return Invalid();
        var sort = query.ContainsKey("sort") ? query["sort"].ToString() : "recent";
        if (!Sorts.Contains(sort, StringComparer.Ordinal)) return Invalid();

        string? folded = null;
        if (query.ContainsKey("q"))
        {
            var text = query["q"].ToString();
            if (text.EnumerateRunes().Count() > 200) return Invalid();
            var value = SearchTextNormalizer.Fold(text);
            folded = value.Length == 0 ? null : value;
        }

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

        request = new(tab, sort, folded, after, before, limit);
        return null;
    }

    public static StatsError Invalid() => new("stats.invalid_request", "Невалідні параметри запиту.");
}
