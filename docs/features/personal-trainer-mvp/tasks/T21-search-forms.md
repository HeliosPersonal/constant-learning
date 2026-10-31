---
id: T21
title: "Генерувати форми пошуку для опублікованих версій"
layer: "infra"
deps: ["T20", "T07"]
acs: ["AC-30", "AC-32"]
files_hint: ["ConstantLearning/Features/Content/Search/Forms/", "ConstantLearning.Tests/Content/Search/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T21 — Генерувати форми пошуку для опублікованих версій

**Шар:** `infra` · **Залежить від:** [T20](./T20-dictionary-import.md), [T07](./T07-migrate-search-jobs-dispatches.md) · **AC:** AC-30, AC-32 · **Оцінка:** M · **Фаза:** 7

## Why

[ADR-0012](../adr/0012-search-via-a-precomputed-table-of-folded-word-forms.md); PRD OQ-6 щодо ліцензії VESUM.

## What

Завантаження польського морфологічного лексикону в `morph_forms`, згортання діакритики, перебудова `search_forms` під час публікації; українською — лише записані переклади й початок слова, доки OQ-6 відкрите.

Файли: `ConstantLearning/Features/Content/Search/Forms/`, `ConstantLearning.Tests/Content/Search/`

## Definition of Done

- [ ] тест: публікація нової версії замінює її форми, знята з публікації — прибирає
- [ ] форма з діакритикою знаходиться без неї
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

Не використовувати VESUM до рішення OQ-6.
