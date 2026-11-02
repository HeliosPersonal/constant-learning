using System.Text;

namespace ConstantLearning.Features.Content.References;

public sealed record IpaConversion(string? Value, string? ManualReason);

/// <summary>Наближена підказка українською; невідомі знаки та носові голосні потребують редактора.</summary>
public sealed class PolishIpaConverter
{
    private static readonly (string Ipa, string Ukrainian)[] Tokens =
    [
        ("t͡ɕ", "ць"), ("d͡ʑ", "дзь"), ("t͡ʂ", "ч"), ("d͡ʐ", "дж"),
        ("t͡s", "ц"), ("d͡z", "дз"), ("t͜ɕ", "ць"), ("d͜ʑ", "дзь"),
        ("t͜ʂ", "ч"), ("d͜ʐ", "дж"), ("t͜s", "ц"), ("d͜z", "дз"),
        ("ɕ", "сь"), ("ʑ", "зь"), ("ɲ", "нь"), ("ʂ", "ш"), ("ʐ", "ж"),
        ("a", "а"), ("ɛ", "е"), ("e", "е"), ("i", "і"), ("ɨ", "и"),
        ("ɔ", "о"), ("o", "о"), ("u", "у"), ("p", "п"), ("b", "б"),
        ("t", "т"), ("d", "д"), ("k", "к"), ("ɡ", "ґ"), ("g", "ґ"),
        ("f", "ф"), ("v", "в"), ("s", "с"), ("z", "з"), ("x", "х"),
        ("m", "м"), ("n", "н"), ("l", "л"), ("r", "р"), ("j", "й"),
        ("w", "в"), ("ʲ", "ь")
    ];

    public IpaConversion Convert(string ipa)
    {
        if (string.IsNullOrWhiteSpace(ipa) || ipa.Length > 500) return new(null, "invalid_ipa");
        var text = ipa.Trim().Normalize(NormalizationForm.FormC);
        if ((text.StartsWith('/') && text.EndsWith('/')) || (text.StartsWith('[') && text.EndsWith(']')))
            text = text[1..^1];
        var result = new StringBuilder();
        var stress = false;
        var seenStress = false;
        var vowels = 0;
        for (var index = 0; index < text.Length;)
        {
            if (text[index] == 'ˈ')
            {
                if (seenStress) return new(null, "ambiguous_ipa_stress");
                stress = true;
                seenStress = true;
                index++;
                continue;
            }
            if (text[index] == '.') { index++; continue; }
            var matched = false;
            foreach (var (token, ukrainian) in Tokens)
            {
                if (!text.AsSpan(index).StartsWith(token, StringComparison.Ordinal)) continue;
                result.Append(ukrainian);
                var isVowel = "аеиіоу".Contains(ukrainian, StringComparison.Ordinal);
                if (isVowel) vowels++;
                if (stress && isVowel)
                {
                    result.Append('\u0301');
                    stress = false;
                }
                index += token.Length;
                matched = true;
                break;
            }
            if (!matched) return new(null, "unsupported_ipa_symbol");
        }
        if (vowels > 1 && !seenStress) return new(null, "ipa_stress_missing");
        return result.Length is 0 or > 500 || vowels == 0 || stress ? new(null, "invalid_ipa") : new(result.ToString(), null);
    }
}
