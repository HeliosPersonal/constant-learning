---
id: T08
title: "Додати агрегований лічильник скидань і метрику латентності"
layer: "app"
deps: ["[T04](./T04-reset-endpoint.md)"]
acs: []
files_hint: ["ConstantLearning/Features/Identity/ProgressReset/", "ConstantLearning.Tests/Identity/ProgressReset/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T08 — Додати агрегований лічильник скидань і метрику латентності

**Шар:** `app` · **Залежить від:** [T04](./T04-reset-endpoint.md) · **AC:** — · **Оцінка:** S

## Why

[PRD §6.1, §7](../PRD.md#7-metrics--kpis): лічильник без персональних даних і p95 скидання.

## What

Серверний лічильник успішних, порожніх і невдалих скидань та невдалих перевірок цілісності та гістограма тривалості без `learner_id` і Telegram-ідентифікатора в мітках.

Файли: `ConstantLearning/Features/Identity/ProgressReset/`, `ConstantLearning.Tests/Identity/ProgressReset/`

## Definition of Done

- [ ] тест перевіряє, що мітки не містять ідентифікаторів, а лічильник збільшується за результатом
- [ ] `dotnet build`, тести й лінтер без нових попереджень
