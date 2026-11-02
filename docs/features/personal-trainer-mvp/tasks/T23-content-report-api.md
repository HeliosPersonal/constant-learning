---
id: T23
title: "Реалізувати надсилання content report з лімітом і дедуплікацією"
layer: "app"
deps: ["[T06](T06-migrate-reports-publication.md)", "[T07](T07-migrate-search-jobs-dispatches.md)", "[T12](T12-initdata-auth.md)"]
acs: ["AC-34", "AC-36", "AC-46", "AC-51"]
files_hint: ["ConstantLearning/Features/Content/Reports/", "ConstantLearning/Features/Identity/Aliases/", "ConstantLearning.Tests/Content/Reports/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T23 — Реалізувати надсилання content report з лімітом і дедуплікацією

**Шар:** `app` · **Залежить від:** [T06](./T06-migrate-reports-publication.md), [T07](./T07-migrate-search-jobs-dispatches.md), [T12](./T12-initdata-auth.md) · **AC:** AC-34, AC-36, AC-46, AC-51 · **Оцінка:** M · **Фаза:** 7

## Why

US-09 у [PRD](../PRD.md); потік «Content report і автоматична звірка» в [sad §6](../sad.md); `createContentReport`; [data-model.md](../data-model.md) `dedup_key`.

## What

Валідація, псевдонімна мітка автора, `dedup_key` і повернення наявного незакритого report, ліміт 20 на добу learner без `answer_dispute`, маршрутизація: задача звірки чи черга редактора.

Файли: `ConstantLearning/Features/Content/Reports/`, `ConstantLearning/Features/Identity/Aliases/`, `ConstantLearning.Tests/Content/Reports/`

## Definition of Done

- [ ] інтеграційні тести AC-34, AC-36, AC-46, AC-51 зелені
- [ ] подвійне надсилання створює один report і одну задачу звірки
- [ ] `dotnet build`, тести й лінтер без нових попереджень
