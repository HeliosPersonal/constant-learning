---
id: T07
title: "Додати тест повноти скидання"
layer: "tests"
deps: ["[T02](./T02-learning-reset-handler.md)", "[T03](./T03-content-reset-handler.md)"]
acs: ["AC-01", "AC-10"]
files_hint: ["ConstantLearning.Tests/Architecture/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T07 — Додати тест повноти скидання

**Шар:** `tests` · **Залежить від:** [T02](./T02-learning-reset-handler.md), [T03](./T03-content-reset-handler.md) · **AC:** AC-01, AC-10 · **Оцінка:** S

## Why

Ризик «новий модуль без обробника» з [SAD §11](../sad.md#11-risks-and-technical-debt).

## What

Архітектурний тест перебирає сутності EF, що посилаються на learner або його слова, і вимагає для кожної обробник скидання або запис у явному списку збережених (профіль, налаштування, reports, відправлення).

Файли: `ConstantLearning.Tests/Architecture/`

## Definition of Done

- [ ] тест падає, якщо додати сутність learner без обробника чи запису в список збереженого
- [ ] `dotnet build`, тести й лінтер без нових попереджень
