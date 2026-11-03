---
id: T04
title: "Обмежити лічильники, нагадування й початок заняття видом plan"
layer: "app"
deps: ["[T01](./T01-session-kind-expand.md)"]
acs: ["AC-17", "AC-18"]
files_hint: ["ConstantLearning/Features/Learning/Today/TodayService.cs", "ConstantLearning/Features/Learning/Notifications/ReminderLearningReader.cs", "ConstantLearning/Features/Learning/Sessions/Start/StartSessionService.cs", "ConstantLearning/Features/Learning/Sessions/DayBoundary/", "ConstantLearning/Features/Learning/Sessions/Current/", "ConstantLearning.Tests/Learning/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T04 — Обмежити лічильники, нагадування й початок заняття видом plan

**Шар:** `app` · **Залежить від:** [T01](./T01-session-kind-expand.md) · **AC:** [AC-17](../PRD.md#ac-17---domain-invariant), [AC-18](../PRD.md#ac-18---domain-invariant) · **Оцінка:** M

## Why

[SAD §11](../sad.md#11-risks-and-technical-debt): пропущений фільтр kind = plan дає тихий регрес; [ADR-0001](../adr/0001-run-extras-as-typed-sessions-over-the-existing-session-pipeline.md).

## What

У всіх запитах, що рахують заняття (completed за 7 днів, нагадування, межа доби плану), додати фільтр `kind = plan`. Початок звичайного заняття при активній extra-session закриває її як незавершену, отримане слово лишається отриманим. Перевірити, що облік денного ліміту (слова, показані сьогодні) коректний після extra.

Файли: `ConstantLearning/Features/Learning/Today/TodayService.cs`, `ConstantLearning/Features/Learning/Notifications/ReminderLearningReader.cs`, `ConstantLearning/Features/Learning/Sessions/Start/StartSessionService.cs`, `ConstantLearning/Features/Learning/Sessions/DayBoundary/`, `ConstantLearning/Features/Learning/Sessions/Current/`, `ConstantLearning.Tests/Learning/`

## Definition of Done

- [ ] додаткові дії не змінюють completed за 7 днів і правила нагадувань (AC-18 — інтеграційний тест)
- [ ] старт плану закриває активну extra-session (інтеграційний тест)
- [ ] наявні тести Today, нагадувань і session зелені
- [ ] `dotnet build` і тести без нових попереджень
