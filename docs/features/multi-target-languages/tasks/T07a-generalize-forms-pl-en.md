---
id: T07a
title: "Узагальнення пошуку форм і нормалізації (зняти хардкод \"pl\") + профіль en"
layer: "domain"
deps: ["[T05](./T05-migrate-content-entries.md)", "[T07s](./T07s-spike-language-sources.md)"]
acs: ["AC-08"]
files_hint:
  - "ConstantLearning/Features/Content/Search/Forms/"
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "done"
---

# T07a — Узагальнення пошуку форм і нормалізації (зняти хардкод `"pl"`) + профіль en

**Шар:** domain · **Залежить від:** [T05](./T05-migrate-content-entries.md), [T07s](./T07s-spike-language-sources.md) · **AC:** AC-08 · **Оцінка:** M · **Фаза:** 3 (Content)

> Перша з двох реалізаційних задач, на які розбито узагальнення форм/нормалізації (друга —
> [T07b](./T07b-full-rules-de-nl.md), правила de/nl). Spike джерел і профілів — [T07s](./T07s-spike-language-sources.md).
> Розбиття заздалегідь (не умовне) тримає кожен PR ≤500 LOC.

## Why

Наявний мово-свідомий субстрат пошуку (`search_forms.lang`/`morph_forms.lang`) перевикористовується,
але код містить хардкод `"pl"` у `SearchFormsProjection`/морфологічному імпортері
([ADR-0004](../adr/0004-model-supported-languages-as-config-table.md), [SAD §4 стовп 4](../sad.md),
[data-model.md §Indexes](../data-model.md)). AC-08: підказка, приклад, прийняті варіанти й нормалізація
відповіді мають відповідати активній мові, а не польській. Ця задача знімає хардкод і доводить
узагальнення на двох мовах (pl + en), не беручись за складні правила de/nl.

## What

- Зняти хардкод `"pl"`: `SearchFormsProjection` і морфологічний імпортер беруть мову з
  `target_languages.normalization_profile` / контексту запису, не з константи.
- Нормалізація відповіді застосовує правила профілю активної мови (діспетч за `normalization_profile`).
- Реалізувати профіль **en** (на джерелах, зафіксованих у [T07s](./T07s-spike-language-sources.md)).
- Профіль **pl** лишається робочим (регресія не допускається).
- Каркас діспетчу профілів готовий приймати de/nl ([T07b](./T07b-full-rules-de-nl.md)) без зміни узагальненого коду.

## Definition of Done

- [x] Жодного літерала `"pl"` у `SearchFormsProjection`/імпортері; мова береться з конфігу/контексту запису.
- [x] Integration-тест: пошук за формами й нормалізація для `pl` і `en` дають мово-свідомий результат.
- [x] Регресійний тест `pl` зелений (узагальнення не зламало наявну поведінку).
- [x] PR ≤500 LOC.

## Нотатки реалізації

- **Рішення ухвалено голосуванням 3 агентів — одностайно варіант B** (спільний union-fold + форми з тегом
  мови + морфологія за мовою + реєстр профілів на `normalization_profile`). Варіант A (повний per-language
  діспетч самого fold) ламав інваріант глобального пошуку: до [T13](./T13-set-active-scope-existing.md) запит
  згортається, ще не знаючи, якою мовою знайдеться збіг (напр. «łza» дало б «lza» на боці форм, але «łza» на
  боці запиту). B лишає **один** `SearchTextNormalizer.Fold` — суперсет підстановок усіх мов, продовжуючи вже
  робочий патерн (fold уже згортав і pl `ł→l`, і uk `ґ→г`).
- **Реєстр профілів — `NormalizationProfiles`** (каркас діспетчу за `normalization_profile`, ADR-0004). Кожен
  профіль (`uk`/`pl`/`en`) дає свій набір підстановок; об'єднана мапа `FoldSubstitutions` живить спільний fold.
  **en власних підстановок не додає** — латинська діакритика (café→cafe, naïve→naive) знімається загальним NFD.
  de/nl додаються ([T07b](./T07b-full-rules-de-nl.md)) лише в список `All`: узагальнений код проєкції/імпортера
  не змінюється.
- **Єдиний літерал `"pl"`** лишився тільки в `NormalizationProfiles.Polish` — це і є конфіг-джерело коду мови
  (аналог рядка `target_languages`), на який символьно посилаються проєкція, імпортер і maintenance.
- **`SearchFormsProjection`** тегує `SearchForm.Lang` з `entry.TargetLanguage` (партія може бути змішаною —
  мапа `entryId→target_language`), а морфологію обирає за ключем `(Lang, Lemma)`, тож форми іншої мови не
  просочуються. `"uk"` лишається константою `TranslationLang` (мова пояснень, а не цільова мова).
- **Морфологічний імпортер PoliMorf** і `GetMissingPolishMorphologyAsync` беруть код мови з
  `NormalizationProfiles.Polish.Code`. Індикатор «без польської морфології» додатково scoped на
  `entry.TargetLanguage == "pl"` — інакше кожен en/de/nl-запис хибно рахувався б як без польської морфології
  (латентний баг мультимовності усунено принагідно).
- **Нормалізація відповіді (AnswerNormalizer, шар Learning) свідомо не чіпалась** — поза `files_hint`
  (`Content/Search/Forms/`) і роздула б PR. Реєстр профілів дає готовий каркас діспетчу за
  `normalization_profile`, який споживач у Learning під'єднає окремо (разом із правилами de/nl у
  [T07b](./T07b-full-rules-de-nl.md)).
- **Інтеримний дефолт `DictionaryEntry.TargetLanguage = "pl"`** (з `TODO(T07a)`) лишено: він і є «контекстом
  запису», який читає проєкція. Зробити властивість `required` і прибрати дефолт означає зачепити всі місця
  створення записів (import-сервіс тощо) — поза обсягом цієї задачі; виноситься в подальше прибирання.
- **Тести:** `Publish_TagsFormsAndMorphologyByEntryTargetLanguage_WithEnglishAlongsidePolish` доводить теги
  форм/морфологію/згортання для en, ізоляцію від pl-«витоку» та pl-регресію в тій самій БД; наявні pl-тести
  (`Publish_Indexes…`, `PackagedPolimorf…`, нормалізатор) лишаються зеленими (13 Forms + 12 search/normalizer).
