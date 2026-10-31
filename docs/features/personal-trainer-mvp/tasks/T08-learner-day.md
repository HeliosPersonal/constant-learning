---
id: T08
title: "Реалізувати обчислення доби learner зі зміною поясу"
layer: "domain"
deps: ["T01"]
acs: ["AC-07", "AC-54"]
files_hint: ["ConstantLearning/Features/Learning/Domain/LearnerDay/", "ConstantLearning.Tests/Learning/Domain/LearnerDay/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T08 — Реалізувати обчислення доби learner зі зміною поясу

**Шар:** `domain` · **Залежить від:** [T01](./T01-module-skeleton.md) · **AC:** AC-07, AC-54 · **Оцінка:** M · **Фаза:** 2

## Why

Правила «Доба learner» і «Зміна часового поясу» з [PRD §5](../PRD.md#5-acceptance-criteria); [ADR-0006](../adr/0006-model-learner-time-with-nodatime-and-iana-zones.md).

## What

Чистий доменний сервіс: поточна доба learner, її межі в UTC з урахуванням DST, кінець доби за поясом початку, подовження до опівночі нового поясу, Варшава як запасний пояс.

Файли: `ConstantLearning/Features/Learning/Domain/LearnerDay/`, `ConstantLearning.Tests/Learning/Domain/LearnerDay/`

## Definition of Done

- [ ] unit-тести з `FakeClock` на AC-07 (Варшава → Токіо) і AC-54 (Варшава → Нью-Йорк) зелені
- [ ] тести на перехід на літній і зимовий час зелені
- [ ] сервіс не звертається до БД
- [ ] `dotnet build`, тести й лінтер без нових попереджень
