---
id: T02
title: "Додати обробник скидання в Learning"
layer: "app"
deps: ["[T01](./T01-reset-flow-and-port.md)", "[T03](./T03-content-reset-handler.md)"]
acs: ["AC-01", "AC-07", "AC-08"]
files_hint: ["ConstantLearning/Features/Learning/Reset/", "ConstantLearning.Tests/Learning/Reset/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T02 — Додати обробник скидання в Learning

**Шар:** `app` · **Залежить від:** [T01](./T01-reset-flow-and-port.md), [T03](./T03-content-reset-handler.md) · **AC:** AC-01, AC-07, AC-08 · **Оцінка:** M

## Why

Дані прогресу належать Learning: [data-model](../../personal-trainer-mvp/data-model.md), [ADR-0008](../../personal-trainer-mvp/adr/0008-keep-the-learning-session-server-side-with-per-card-idempotency.md).

## What

Обробник видаляє стан слів, session із картками, відповіді та нейтралізації learner. Порядок видалення враховує зовнішні ключі (відповіді посилаються на картки; reports знімає окремий обробник Content). Виконується після обробника Content ([T03](./T03-content-reset-handler.md)): без зняття привʼязки reports видалення відповідей порушить зовнішній ключ. Реєструється в `LearningModule`.

Файли: `ConstantLearning/Features/Learning/Reset/`, `ConstantLearning.Tests/Learning/Reset/`

## Definition of Done

- [ ] після скидання немає слів на повторення, session і відповідей learner (AC-07, AC-08); тести проходять
- [ ] `dotnet build`, тести й лінтер без нових попереджень
