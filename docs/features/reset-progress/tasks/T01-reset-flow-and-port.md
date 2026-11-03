---
id: T01
title: "Реалізувати потік скидання й порт обробників в Identity"
layer: "app"
deps: []
acs: ["AC-01", "AC-04", "AC-05", "AC-06", "AC-09"]
files_hint: ["ConstantLearning/Features/Identity/ProgressReset/", "ConstantLearning.Tests/Identity/ProgressReset/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T01 — Реалізувати потік скидання й порт обробників в Identity

**Шар:** `app` · **Залежить від:** — · **AC:** AC-01, AC-04, AC-05, AC-06, AC-09 · **Оцінка:** M

## Why

[ADR-0001](../adr/0001-erase-learner-progress-in-one-transaction-under-learner-lock.md), потік у [SAD §6](../sad.md#6-runtime-view).

## What

Порт обробника скидання за зразком `ILearnerSettingsChangedHandler` і сервіс Identity: транзакція, блокування learner через `ILearnerDayStateService.GetForUpdateAsync`, виклик обробників, скидання полів learner, що належать прогресу (маркер завершення курсу, облік виданих сьогодні нових слів), без зміни налаштувань. Порожній прогрес — успіх без змін (`erased: false`). Обробники виконуються в порядку Content, Learning. Після них сервіс перевіряє, що даних learner не лишилося; інакше відкочує транзакцію й збільшує лічильник невдалих перевірок. Збій обробника відкочує все.

Файли: `ConstantLearning/Features/Identity/ProgressReset/`, `ConstantLearning.Tests/Identity/ProgressReset/`

## Definition of Done

- [ ] інтеграційні тести AC-01, AC-04, AC-05, AC-06, AC-09 зелені на сервісі з тестовим обробником
- [ ] `dotnet build`, тести й лінтер без нових попереджень
