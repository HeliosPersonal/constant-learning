---
id: T19
title: "Реалізувати екран «Сьогодні» на сервері"
layer: "app"
deps: ["[T16](T16-session-continuation.md)"]
acs: ["AC-10", "AC-28", "AC-29", "AC-33"]
files_hint: ["ConstantLearning/Features/Learning/Today/", "ConstantLearning.Tests/Learning/Today/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T19 — Реалізувати екран «Сьогодні» на сервері

**Шар:** `app` · **Залежить від:** [T16](./T16-session-continuation.md) · **AC:** AC-10, AC-28, AC-29, AC-33 · **Оцінка:** S · **Фаза:** 7

## Why

US-07 у [PRD](../PRD.md); потік «Екран «Сьогодні»» у [sad §6](../sad.md); `getToday`.

## What

Лічильники слів до повторення, recalled words, completed sessions за 7 дат, стан курсу й активної session — лише для поточного learner.

Файли: `ConstantLearning/Features/Learning/Today/`, `ConstantLearning.Tests/Learning/Today/`

## Definition of Done

- [ ] інтеграційні тести AC-28, AC-29 зелені
- [ ] слово запису без `approved` не рахується в повтореннях і recalled words (AC-33)
- [ ] бенчмарк: на фікстурі з 1 000 слів learner p95 відповіді сервера ≤ 300 ms
- [ ] `dotnet build`, тести й лінтер без нових попереджень
