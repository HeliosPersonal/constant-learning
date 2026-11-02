---
id: T22
title: "Реалізувати пошук у словнику й картку запису"
layer: "ports"
deps: ["[T21](T21-search-forms.md)", "[T12](T12-initdata-auth.md)"]
acs: ["AC-30", "AC-31", "AC-32", "AC-33"]
files_hint: ["ConstantLearning/Features/Content/Search/Api/", "ConstantLearning.Tests/Content/Search/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T22 — Реалізувати пошук у словнику й картку запису

**Шар:** `ports` · **Залежить від:** [T21](./T21-search-forms.md), [T12](./T12-initdata-auth.md) · **AC:** AC-30, AC-31, AC-32, AC-33 · **Оцінка:** M · **Фаза:** 8

## Why

US-08 у [PRD](../PRD.md); потік «Пошук у словнику» в [sad §6](../sad.md); `searchDictionary`, `getDictionaryEntry`.

## What

Точний збіг для 1 символу, точний і префіксний від 2 символів, порядок «точний збіг, далі base course», `CancellationToken` до EF-запиту, word status learner у результатах.

Файли: `ConstantLearning/Features/Content/Search/Api/`, `ConstantLearning.Tests/Content/Search/`

## Definition of Done

- [ ] інтеграційні тести AC-30…AC-33 зелені
- [ ] бенчмарк: 500 запитів по 1–5 символів на фікстурі з 2 000 записів — p95 сервера ≤ 300 ms
- [ ] `dotnet build`, тести й лінтер без нових попереджень
