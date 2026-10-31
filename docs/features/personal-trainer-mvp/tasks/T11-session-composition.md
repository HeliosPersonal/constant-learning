---
id: T11
title: "Реалізувати складання session з лімітами й квотами"
layer: "domain"
deps: ["T08", "T10"]
acs: ["AC-03", "AC-04", "AC-05", "AC-06", "AC-09", "AC-10"]
files_hint: ["ConstantLearning/Features/Learning/Domain/Composition/", "ConstantLearning.Tests/Learning/Domain/Composition/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T11 — Реалізувати складання session з лімітами й квотами

**Шар:** `domain` · **Залежить від:** [T08](./T08-learner-day.md), [T10](./T10-review-scheduler.md) · **AC:** AC-03, AC-04, AC-05, AC-06, AC-09, AC-10 · **Оцінка:** M · **Фаза:** 3

## Why

Правило «Склад session» з [PRD §5](../PRD.md#5-acceptance-criteria), потік «Складання session» у [sad §6](../sad.md).

## What

Чиста функція складу: ≤20 карток, спершу найпростроченіші повторення, нові слова в межах залишку денного ліміту (з урахуванням «Вже знаю» понад 10), чергування квот за найменшою часткою, стан «курс завершено».

Файли: `ConstantLearning/Features/Learning/Domain/Composition/`, `ConstantLearning.Tests/Learning/Domain/Composition/`

## Definition of Done

- [ ] unit-тести AC-03…AC-06, AC-09, AC-10 зелені
- [ ] квоти при рівності часток віддають місце `everyday`
- [ ] `dotnet build`, тести й лінтер без нових попереджень
