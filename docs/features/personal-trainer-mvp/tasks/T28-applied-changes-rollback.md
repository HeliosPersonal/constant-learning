---
id: T28
title: "Реалізувати список застосованих змін і відкат"
layer: "app"
deps: ["[T27](T27-editor-queue.md)"]
acs: ["AC-48", "AC-49", "AC-50"]
files_hint: ["ConstantLearning/Features/Content/Editor/Rollback/", "ConstantLearning.Tests/Content/Editor/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T28 — Реалізувати список застосованих змін і відкат

**Шар:** `app` · **Залежить від:** [T27](./T27-editor-queue.md) · **AC:** AC-48, AC-49, AC-50 · **Оцінка:** M · **Фаза:** 9

## Why

US-12 у [PRD](../PRD.md); потік «Відкат застосованої зміни словника» в [sad §6](../sad.md); `listAppliedChanges`, `rollbackAppliedChange`; правила відкату в [data-model.md](../data-model.md).

## What

Список публікацій звірки й редактора з content report, відкат лише поточної версії з оптимістичною перевіркою вказівника, рядок `rollback` у журналі, перебудова форм пошуку.

Файли: `ConstantLearning/Features/Content/Editor/Rollback/`, `ConstantLearning.Tests/Content/Editor/`

## Definition of Done

- [ ] інтеграційні тести AC-48, AC-49, AC-50 зелені
- [ ] відкат не змінює word status, відповіді й закритий report
- [ ] `dotnet build`, тести й лінтер без нових попереджень
