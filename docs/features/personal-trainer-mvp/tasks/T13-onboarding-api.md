---
id: T13
title: "Реалізувати онбординг і профіль learner"
layer: "app"
deps: ["[T12](T12-initdata-auth.md)"]
acs: ["AC-01", "AC-02", "AC-53"]
files_hint: ["ConstantLearning/Features/Identity/Onboarding/", "ConstantLearning/Features/Identity/Learners/", "ConstantLearning.Tests/Identity/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T13 — Реалізувати онбординг і профіль learner

**Шар:** `app` · **Залежить від:** [T12](./T12-initdata-auth.md) · **AC:** AC-01, AC-02, AC-53 · **Оцінка:** M · **Фаза:** 4

## Why

US-01 у [PRD](../PRD.md); потік «Онбординг learner» у [sad §6](../sad.md); операції `completeOnboarding`, `getCurrentLearner` в [openapi.yaml](../contracts/openapi.yaml).

## What

Створення learner з лімітом, поясом (Варшава й `time_zone_confirmed = false`, якщо пояс невідомий) і необов'язковим reminder; повторний онбординг повертає наявного learner без змін.

Файли: `ConstantLearning/Features/Identity/Onboarding/`, `ConstantLearning/Features/Identity/Learners/`, `ConstantLearning.Tests/Identity/`

## Definition of Done

- [ ] інтеграційні тести AC-01, AC-02, AC-53 зелені
- [ ] відповіді збігаються з прикладами `completeOnboarding` (201 і 200)
- [ ] `dotnet build`, тести й лінтер без нових попереджень
