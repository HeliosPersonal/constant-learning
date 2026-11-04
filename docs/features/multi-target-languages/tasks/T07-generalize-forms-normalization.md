---
id: T07
title: "Узагальнення пошуку форм і нормалізації (зняти хардкод pl)"
layer: "domain"
deps: ["[T05](./T05-migrate-content-entries.md)"]
acs: ["AC-08"]
files_hint:
  - "ConstantLearning/Features/Content/Search/Forms/"
owner: "<TBD Tech Lead>"
estimate: "L"
status: "blocked"
---

# T07 — Узагальнення пошуку форм і нормалізації (зняти хардкод `"pl"`)

**Шар:** domain · **Залежить від:** [T05](./T05-migrate-content-entries.md) · **AC:** AC-08 · **Оцінка:** L · **Фаза:** 3 (Content)

> **Blocked — OQ-5** (owner: Tech Lead): чи узагальнюється звірка відповіді й генерація словоформ
> на de/nl (рід `der/die/das`, `de/het`, капіталізація, складені слова). Каркас `pl`/`en` не блокується;
> de/nl лишаються неповними по AC-08 до рішення. Див. [PRD §8 OQ-5](../PRD.md#8-open-questions).

## Why

Наявний мово-свідомий субстрат пошуку (`search_forms.lang`/`morph_forms.lang`) перевикористовується,
але код містить хардкод `"pl"` у `SearchFormsProjection`/морфологічному імпортері
([ADR-0004](../adr/0004-model-supported-languages-as-config-table.md), [SAD §4 стовп 4](../sad.md),
[data-model.md §Indexes](../data-model.md)). AC-08: підказка, приклад, прийняті варіанти й нормалізація
відповіді мають відповідати активній мові, а не польській.

## What

- Зняти хардкод `"pl"`: `SearchFormsProjection` і морфологічний імпортер беруть мову з
  `target_languages.normalization_profile` / контексту запису, не з константи.
- Нормалізація відповіді застосовує правила профілю активної мови.
- Для мови з `normalization_profile = NULL` (de/nl до OQ-5) — чітка деградація: базова нормалізація
  без мово-специфічних правил, AC-08 позначена неповною (не падіння).

## Definition of Done

- [ ] Жодного літерала `"pl"` у `SearchFormsProjection`/імпортері; мова береться з конфігу.
- [ ] Integration-тест: пошук за формами й нормалізація для `pl` і ще однієї enabled-мови дають мово-свідомий результат.
- [ ] Запис мови з профілем NULL обробляється базовою нормалізацією без винятку.
- [ ] OQ-5 закрито Tech Lead (або явно зафіксовано, що de/nl лишаються неповними по AC-08).
- [ ] PR ≤500 LOC.
