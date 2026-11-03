---
id: T06
title: "Реалізувати старт «Ще одне слово»: session extra_word, добір слова, ідемпотентність, захист від зловживань"
layer: "app"
deps: ["[T01](./T01-session-kind-expand.md)", "[T04](./T04-plan-kind-filters.md)"]
acs: ["AC-01", "AC-02", "AC-03", "AC-05", "AC-06"]
files_hint: ["ConstantLearning/Features/Learning/Sessions/Extra/", "ConstantLearning/Features/Learning/Sessions/NewWords/", "ConstantLearning.Tests/Learning/Sessions/"]
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T06 — Реалізувати старт «Ще одне слово»: session extra_word, добір слова, ідемпотентність, захист від зловживань

**Шар:** `app` · **Залежить від:** [T01](./T01-session-kind-expand.md), [T04](./T04-plan-kind-filters.md) · **AC:** [AC-01](../PRD.md#ac-01---happy-path), [AC-02](../PRD.md#ac-02---domain-invariant), [AC-03](../PRD.md#ac-03---happy-path), [AC-05](../PRD.md#ac-05---domain-invariant), [AC-06](../PRD.md#ac-06---cross-context) · **Оцінка:** L

## Why

[PRD US-01](../PRD.md#4-user-stories), [SAD §6](../sad.md#ще-одне-слово), [ADR-0001](../adr/0001-run-extras-as-typed-sessions-over-the-existing-session-pipeline.md).

## What

Старт створює session `extra_word` з карткою нового слова (наступне за порядком вибору базового PRD без ліміту) і перевіркою; повторний старт повертає ту саму активну extra-session. Картка нового слова для extra не додає слово на заміну й не збільшує плановий облік ліміту; «Вже знаю» рахує заміну за добу. Слова без `approved` пропускаються. Технічний захист: не більше 30 стартів на хвилину на learner.

Файли: `ConstantLearning/Features/Learning/Sessions/Extra/`, `ConstantLearning/Features/Learning/Sessions/NewWords/`, `ConstantLearning.Tests/Learning/Sessions/`

## Definition of Done

- [ ] інтеграційні тести AC-01, AC-02, AC-03, AC-05, AC-06 зелені на тимчасовій БД
- [ ] подвійне й паралельне натискання не дає дублів і пропусків
- [ ] 31-й старт за хвилину просить зачекати, навчальної межі немає
- [ ] `dotnet build` і тести без нових попереджень
