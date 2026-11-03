---
id: T04
title: "Додати дію скидання прогресу в API й зареєструвати код помилки"
layer: "ports"
deps: ["[T01](./T01-reset-flow-and-port.md)", "[T02](./T02-learning-reset-handler.md)", "[T03](./T03-content-reset-handler.md)"]
acs: ["AC-01", "AC-04", "AC-05", "AC-09"]
files_hint: ["ConstantLearning/Features/Identity/ProgressReset/", "ConstantLearning.Tests/Infrastructure/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T04 — Додати дію скидання прогресу в API й зареєструвати код помилки

**Шар:** `ports` · **Залежить від:** [T01](./T01-reset-flow-and-port.md), [T02](./T02-learning-reset-handler.md), [T03](./T03-content-reset-handler.md) · **AC:** AC-01, AC-04, AC-05, AC-09 · **Оцінка:** S

## Why

Контракт: [openapi](../contracts/openapi.yaml), звіт: [api-sync-report](../contracts/api-sync-report.md).

## What

Контролер `resetLearnerProgress` у Identity з `learner_id` лише з claims; відповіді `erased: true/false`, 401 і 404 як у контракті; збій — помилка з кодом `progress_reset.failed` у реєстрі помилок Identity. Тест контракту звіряє відповіді з openapi.

Файли: `ConstantLearning/Features/Identity/ProgressReset/`, `ConstantLearning.Tests/Infrastructure/`

## Definition of Done

- [ ] тести контракту та авторизації AC-05 зелені; код помилки з контракту визначено в реєстрі
- [ ] `dotnet build`, тести й лінтер без нових попереджень
