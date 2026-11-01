using System.Text.RegularExpressions;

namespace ConstantLearning.Features.Content.References;

public sealed record ReferenceValue(string Field, string Value);
public sealed record ReferenceParseResult(IReadOnlyList<ReferenceValue> Values, IReadOnlyList<ReferenceValue> ManualDecisions);

/// <summary>Обмежена граматика вікітексту без розгортання шаблонів чи виконання коду MediaWiki.</summary>
public sealed partial class WikislownikParser(PolishIpaConverter converter)
{
    public ReferenceParseResult Parse(string wikitext)
    {
        var values = new List<ReferenceValue>();
        var manual = new List<ReferenceValue>();
        if (wikitext.Length > 1_000_000) return Failure("page_too_large");
        try
        {
            var sections = LanguageHeader().Matches(wikitext);
            var polish = sections.Cast<Match>().Where(match => match.Value.Contains("{{język polski}}", StringComparison.Ordinal)).ToArray();
            if (polish.Length != 1) return Failure("missing_or_ambiguous_polish_section");
            var start = polish[0].Index + polish[0].Length;
            var next = sections.Cast<Match>().FirstOrDefault(match => match.Index > polish[0].Index);
            var section = wikitext[start..(next?.Index ?? wikitext.Length)];
            var pronunciation = Block(section, "wymowa");
            var ipas = IpaTemplate().Matches(pronunciation).Select(match => match.Groups[1].Value.Trim()).Distinct().ToArray();
            if (ipas.Length != 1 || ipas[0].Length > 500 || IpaStart().Matches(pronunciation).Count != 1)
                manual.Add(new(ReferenceFields.Transcription, "missing_or_ambiguous_ipa"));
            else
            {
                values.Add(new("ipa", ipas[0]));
                var converted = converter.Convert(ipas[0]);
                if (converted.Value is not null) values.Add(new(ReferenceFields.Transcription, converted.Value));
                else manual.Add(new(ReferenceFields.Transcription, converted.ManualReason!));
            }

            var translations = UkrainianLine().Matches(Block(section, "tłumaczenia"));
            var senses = DefinitionSense().Matches(Block(section, "znaczenia")).Select(match => match.Groups[1].Value).Distinct().ToArray();
            if (senses.Length != 1)
                manual.Add(new(ReferenceFields.Translation, "ambiguous_translation_senses"));
            else if (translations.Count != 1) manual.Add(new(ReferenceFields.Translation, "missing_or_ambiguous_ukrainian_translation"));
            else
            {
                var line = translations[0].Groups[1].Value.Trim();
                var sense = SensePrefix().Match(line);
                if (!sense.Success || sense.Groups[1].Value != senses[0]) manual.Add(new(ReferenceFields.Translation, "unsupported_translation_senses"));
                else
                {
                    line = line[sense.Length..].Trim();
                    var links = TranslationLink().Matches(line);
                    var remainder = TranslationLink().Replace(line, "").Trim(' ', ',', ';');
                    if (links.Count == 0 || remainder.Length > 0)
                        manual.Add(new(ReferenceFields.Translation, "unsupported_translation_markup"));
                    else
                        values.AddRange(links.Select(match => new ReferenceValue(ReferenceFields.Translation, match.Groups[1].Value)));
                }
            }
            return new(values.Distinct().ToArray(), manual);
        }
        catch (RegexMatchTimeoutException) { return Failure("parser_timeout"); }
    }

    private static string Block(string section, string name)
    {
        var marker = "{{" + name + "}}";
        var start = section.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0 || section.IndexOf(marker, start + marker.Length, StringComparison.Ordinal) >= 0) return "";
        start += marker.Length;
        var next = SectionMarker().Match(section, start);
        return section[start..(next.Success ? next.Index : section.Length)];
    }

    private static ReferenceParseResult Failure(string reason) => new([], [
        new(ReferenceFields.Translation, reason), new(ReferenceFields.Transcription, reason)]);

    [GeneratedRegex(@"(?m)^==[^=\r\n]+==\s*$", RegexOptions.None, 1000)]
    private static partial Regex LanguageHeader();
    [GeneratedRegex(@"(?m)^\{\{[\p{L} ]+\}\}\s*$", RegexOptions.None, 1000)]
    private static partial Regex SectionMarker();
    [GeneratedRegex(@"\{\{IPA[234]?\|([^{}|]+)\}\}", RegexOptions.None, 1000)]
    private static partial Regex IpaTemplate();
    [GeneratedRegex(@"\{\{IPA", RegexOptions.None, 1000)]
    private static partial Regex IpaStart();
    [GeneratedRegex(@"(?m)^:\s*\((\d+\.\d+)\)", RegexOptions.None, 1000)]
    private static partial Regex DefinitionSense();
    [GeneratedRegex(@"(?m)^\*\s*ukraiński:\s*([^\r\n]+)$", RegexOptions.None, 1000)]
    private static partial Regex UkrainianLine();
    [GeneratedRegex(@"^\((\d+\.\d+)\)\s*", RegexOptions.None, 1000)]
    private static partial Regex SensePrefix();
    [GeneratedRegex(@"\[\[([\p{IsCyrillic}’' -]{1,200})(?:#uk)?\]\]", RegexOptions.None, 1000)]
    private static partial Regex TranslationLink();
}
