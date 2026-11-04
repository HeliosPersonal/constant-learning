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
status: "todo"
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

- [ ] Жодного літерала `"pl"` у `SearchFormsProjection`/імпортері; мова береться з конфігу/контексту запису.
- [ ] Integration-тест: пошук за формами й нормалізація для `pl` і `en` дають мово-свідомий результат.
- [ ] Регресійний тест `pl` зелений (узагальнення не зламало наявну поведінку).
- [ ] PR ≤500 LOC.
