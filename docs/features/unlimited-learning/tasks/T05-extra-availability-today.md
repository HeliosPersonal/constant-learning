---
id: T05
title: "Обчислити доступність extra-дій і додати extra_actions у відповідь екрана «Сьогодні»"
layer: "app"
deps: ["[T01](./T01-session-kind-expand.md)", "[T04](./T04-plan-kind-filters.md)"]
acs: ["AC-04", "AC-10", "AC-16", "AC-17"]
files_hint: ["ConstantLearning/Features/Learning/Today/", "ConstantLearning/Features/Learning/Sessions/Extra/", "ConstantLearning.Tests/Learning/Today/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T05 — Обчислити доступність extra-дій і додати extra_actions у відповідь екрана «Сьогодні»

**Шар:** `app` · **Залежить від:** [T01](./T01-session-kind-expand.md), [T04](./T04-plan-kind-filters.md) · **AC:** [AC-04](../PRD.md#ac-04---error), [AC-10](../PRD.md#ac-10---error), [AC-16](../PRD.md#ac-16---happy-path), [AC-17](../PRD.md#ac-17---domain-invariant) · **Оцінка:** M

## Why

[PRD US-03](../PRD.md#4-user-stories), [SAD §6](../sad.md#екран-сьогодні-після-виконання-плану): доступність вирішує лише сервер.

## What

Новий компонент `Sessions/Extra` з правилом придатності слів (статус «На повторення» або «Вже пригадую», без control repetition, без відповіді сьогодні, не отримані сьогодні, approved) і стану «план виконано». `TodayService` повертає `extra_actions` (`plan_done`, `extra_new_word`, `extra_practice` із причиною недоступності).

Файли: `ConstantLearning/Features/Learning/Today/`, `ConstantLearning/Features/Learning/Sessions/Extra/`, `ConstantLearning.Tests/Learning/Today/`

## Definition of Done

- [ ] компонентні/інтеграційні тести AC-16, AC-17, AC-04, AC-10 зелені
- [ ] причини недоступності: plan_not_done, course_completed, no_eligible_words
- [ ] наявні тести Today зелені
- [ ] `dotnet build` і тести без нових попереджень
