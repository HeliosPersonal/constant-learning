---
id: T15
title: "Реалізувати старт session з персистенцією карток"
layer: "app"
deps: ["[T11](T11-session-composition.md)", "[T05](T05-migrate-learning.md)", "[T04](T04-migrate-content-core.md)", "[T12](T12-initdata-auth.md)"]
acs: ["AC-03", "AC-04", "AC-05", "AC-08"]
files_hint: ["ConstantLearning/Features/Learning/Sessions/Start/", "ConstantLearning.Tests/Learning/Sessions/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T15 — Реалізувати старт session з персистенцією карток

**Шар:** `app` · **Залежить від:** [T11](./T11-session-composition.md), [T05](./T05-migrate-learning.md), [T04](./T04-migrate-content-core.md), [T12](./T12-initdata-auth.md) · **AC:** AC-03, AC-04, AC-05, AC-08 · **Оцінка:** M · **Фаза:** 5

## Why

US-02 у [PRD](../PRD.md); [ADR-0008](../adr/0008-keep-the-learning-session-server-side-with-per-card-idempotency.md); `startSession`.

## What

Читання слів до повторення, виклик складу session ([T11](T11-session-composition.md)), запис `learning_sessions` і `session_items` з добою й кінцем доби; одна активна session на learner.

Файли: `ConstantLearning/Features/Learning/Sessions/Start/`, `ConstantLearning.Tests/Learning/Sessions/`

## Definition of Done

- [ ] інтеграційні тести на PostgreSQL для AC-03, AC-04, AC-05, AC-08 зелені
- [ ] паралельний старт двох session дає одну активну
- [ ] `dotnet build`, тести й лінтер без нових попереджень
