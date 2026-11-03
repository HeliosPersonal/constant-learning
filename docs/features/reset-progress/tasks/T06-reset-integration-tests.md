---
id: T06
title: "Дописати інтеграційні тести збою, одночасності й супутніх сутностей"
layer: "tests"
deps: ["[T04](./T04-reset-endpoint.md)"]
acs: ["AC-04", "AC-06", "AC-10", "AC-11", "AC-12"]
files_hint: ["ConstantLearning.Tests/Identity/ProgressReset/", "ConstantLearning.Tests/Notifications/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T06 — Дописати інтеграційні тести збою, одночасності й супутніх сутностей

**Шар:** `tests` · **Залежить від:** [T04](./T04-reset-endpoint.md) · **AC:** AC-04, AC-06, AC-10, AC-11, AC-12 · **Оцінка:** M

## Why

Тест-план у [PRD](../PRD.md#test-plan): QG-2 і QG-4 з [SAD §10](../sad.md#10-quality-requirements).

## What

На справжній тимчасовій базі: збій посеред скидання не лишає часткового стирання; одночасні скидання й відповідь виконуються по черзі; нагадування й підсумок reports після скидання без стертих відповідей; налаштування й reminder не змінюються.

Файли: `ConstantLearning.Tests/Identity/ProgressReset/`, `ConstantLearning.Tests/Notifications/`

## Definition of Done

- [ ] набір зелений; тест збою доводить повний відкат
- [ ] `dotnet build`, тести й лінтер без нових попереджень
