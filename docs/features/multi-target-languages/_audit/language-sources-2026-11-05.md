# Джерела й ліцензії для en/de/nl — spike T07s (OQ-7)

**Дата:** 2026-11-05
**Стадія:** 13 (implement-tasks) · задача [T07s](../tasks/T07s-spike-language-sources.md)
**Мета:** закрити [OQ-7](../sad.md#11-risks-and-technical-debt) — підтвердити наявність і ліцензійність
вільних per-language джерел (морфологічний лексикон, частотний корпус, довідкове джерело автозвірки)
для **en / de / nl** перед кодуванням правил форм/нормалізації ([T07a](../tasks/T07a-generalize-forms-pl-en.md) /
[T07b](../tasks/T07b-full-rules-de-nl.md)).

> **Контекст продукту:** комерційний застосунок для українськомовних, що вивчають іноземні мови.
> «Придатне» нижче = можна вживати **комерційно**. Share-alike (SA) зобов'язує тримати CC-BY-SA лише на
> **похідних самого набору даних**, що перерозповсюджуються; згенерований контент уроків більшість команд
> трактує як attribution-only — але межу «adaptation vs output» підтверджує юрист перед релізом.

## Зведена таблиця: мова → джерело → ліцензія → покриття

| Мова | Категорія | Джерело (рекомендоване) | Версія | Ліцензія | Покриття |
|---|---|---|---|---|---|
| en | Морфологічний лексикон | [Wiktextract / Kaikki](https://kaikki.org/dictionary/rawdata.html) (en-edition) | тижневі дампи 2026 | CC-BY-SA (+GFDL) | сотні тис. лем із формами, глосами, IPA |
| en | Частотний корпус | [HermitDave FrequencyWords](https://github.com/hermitdave/FrequencyWords) (OpenSubtitles 2018) | 2018 | дані CC-BY-SA-4.0, код MIT | ранжовані форми (50k-списки й більші) |
| en | Довідка автозвірки | [Tatoeba](https://tatoeba.org/en/terms_of_use) `eng` | тижневий експорт | CC-BY-2.0-FR (підмножина CC0-1.0) | мільйони речень, вирівняні переклади |
| de | Морфологічний лексикон | [UniMorph `deu`](https://github.com/unimorph/deu) або Wiktextract (de) | UniMorph 4.0 (2022) | CC-BY-SA-3.0 | сотні тис. форм (ім./прикм./дієсл.) |
| de | Частотний корпус | HermitDave FrequencyWords `de` / [wordfreq](https://github.com/rspeer/wordfreq) | 2018 / 3.x | CC-BY-SA-4.0 (дані) | ~50k ранжованих форм |
| de | Довідка автозвірки | Tatoeba `deu` | тижневий експорт | CC-BY-2.0-FR / CC0 | >1M речень, EN-вирівняні |
| nl | Морфологічний лексикон | Wiktextract / Kaikki (nl) або [UniMorph `nld`](https://github.com/unimorph/nld) | тижневі / UniMorph 4.0 | CC-BY-SA (+GFDL) / CC-BY-SA-3.0 | найбагатша вільна голландська морфологія |
| nl | Частотний корпус | HermitDave FrequencyWords `nl` / wordfreq | 2018 / 3.x | CC-BY-SA-4.0 (дані) | ~50k ранжованих форм |
| nl | Довідка автозвірки | Tatoeba `nld` | тижневий експорт | CC-BY-2.0-FR / CC0 | сотні тис. речень, EN-вирівняні |

**Єдина ліцензійна історія:** увесь рекомендований набір — CC-BY-SA (дані) або CC-BY (Tatoeba). Жодного
NC й жодної платної ліцензії; один розділ attribution + дисципліна share-alike покривають усі три мови.

## Непридатні / ризиковані джерела (відхилено)

| Джерело | Мови | Проблема |
|---|---|---|
| CELEX-2 / e-Lex / RBN | en/de/nl (зокрема голландська) | платна/ліцензована (LDC / INT) — **не вільна**, не вживати |
| GermaNet | de | академічна ліцензія; комерційне вживання — платне |
| SUBTLEX-DE / SUBTLEX-NL | de/nl | «research-free»; чистого комерційного гранту немає — лише з письмовим дозволом автора |
| Leipzig Corpora Collection | усі | неоднозначно: downloads декларують CC-BY, веб-API — CC-BY-NC; перевіряти **покорпусно** перед комерцією |
| UniMorph (стаття) | — | сама **стаття** CC-BY-NC, але **дані** per-language (eng/deu/nld) — CC-BY-SA-3.0 (успадковано з Wiktionary) |

## Рекомендація на мову

- **en:** Wiktextract/Kaikki (морфологія) + HermitDave/wordfreq (частотність) + Tatoeba (довідка).
- **de:** UniMorph `deu` або Wiktextract (морфологія) + HermitDave/wordfreq + Tatoeba. Якщо беремо
  UD German-GSD — лише **≥ v2.5** (у ≤ v2.4 ліцензія CC-BY-NC-SA-3.0). GermaNet і SUBTLEX-DE — уникати.
- **nl:** Wiktextract/Kaikki (найкраща вільна голландська морфологія — CELEX/e-Lex відкинути повністю) +
  HermitDave/wordfreq + Tatoeba. SUBTLEX-NL — лише з письмовим дозволом.

## Профілі нормалізації

`normalization_profile` `'en'`/`'de'`/`'nl'` **уже заведено** (не NULL) у seed
[`20261104090100_seed_target_languages.up.sql`](../migrations/20261104090100_seed_target_languages.up.sql)
разом із `'pl'` (T01). Самі правила нормалізації/форм кодуються в [T07a](../tasks/T07a-generalize-forms-pl-en.md)
(узагальнення pl/en) та [T07b](../tasks/T07b-full-rules-de-nl.md) (повні правила de/nl) — поза обсягом цього spike.

## Межа (AC-02)

Мова без підтвердженого **approved-контенту** лишається `is_enabled=false` і недоступна
([T06](../tasks/T06-base-course-availability.md), AC-02). Spike підтверджує лише доступність **джерел**;
наповнення контентом і увімкнення мови — поза обсягом T07s.

## Що не вдалося повністю підтвердити

- Точна комерційна ліцензія **SUBTLEX-DE / SUBTLEX-NL** — лише «research-free»; публічного CC-гранту на
  DE/NL-варіанти не знайдено. Трактуємо як academic-only (і так відхилено на користь HermitDave/wordfreq).
- Ліцензія завантажень **Leipzig Corpora Collection** суперечлива між офіційними й каталожними джерелами
  (CC-BY vs CC-BY-NC-4.0) — перевіряти покорпусно (відхилено на користь HermitDave/wordfreq).
- Непридатність CELEX / GermaNet / e-Lex — усталений факт, цього разу не перефетчено наживо.

## Джерела

- [UniMorph eng](https://github.com/unimorph/eng) / [deu](https://github.com/unimorph/deu) / [nld](https://github.com/unimorph/nld)
- [Kaikki.org / Wiktextract raw data + license](https://kaikki.org/dictionary/rawdata.html)
- [HermitDave FrequencyWords LICENSE](https://github.com/hermitdave/FrequencyWords/blob/master/LICENSE)
- [wordfreq (Apache-2.0 код, CC-BY-SA-4.0 дані)](https://github.com/rspeer/wordfreq)
- [Tatoeba terms of use](https://tatoeba.org/en/terms_of_use)
- [Leipzig Wortschatz terms](https://wortschatz.uni-leipzig.de/en/usage)
</content>
</invoke>
