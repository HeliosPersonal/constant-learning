---
id: T17
title: "Реалізувати відповідь на картку з ідемпотентністю"
layer: "app"
deps: ["T09", "T10", "T16"]
acs: ["AC-12", "AC-13", "AC-14", "AC-15", "AC-16", "AC-33", "AC-42", "AC-52", "AC-56", "AC-57"]
files_hint: ["ConstantLearning/Features/Learning/Sessions/Answer/", "ConstantLearning.Tests/Learning/Sessions/"]
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T17 — Реалізувати відповідь на картку з ідемпотентністю

**Шар:** `app` · **Залежить від:** [T09](./T09-answer-evaluation.md), [T10](./T10-review-scheduler.md), [T16](./T16-session-continuation.md) · **AC:** AC-12, AC-13, AC-14, AC-15, AC-16, AC-33, AC-42, AC-52, AC-56, AC-57 · **Оцінка:** L · **Фаза:** 7

## Why

US-03 у [PRD](../PRD.md); Critical flow 1 і «Оцінка відповіді й статус слова» в [sad §6](../sad.md); `answerSessionItem`; QG-1.

## What

В одній транзакції: захоплення картки `pending → answered`, оцінка (T09), запис `answer_events`, виклик планувальника (T10) і побудова наступної картки та `session_completed`; повтор з тим самим вмістом повертає збережений результат, з іншим — `answer.already_answered_differently`; картка запису, що втратив `approved`, пропускається без зарахування (AC-33); після межі доби — `session.expired` (AC-52).

Файли: `ConstantLearning/Features/Learning/Sessions/Answer/`, `ConstantLearning.Tests/Learning/Sessions/`

## Definition of Done

- [ ] інтеграційний тест подвійного надсилання дає рівно одну подію відповіді (QG-1)
- [ ] інтеграційні тести AC-12…AC-16, AC-33, AC-52, AC-56, AC-57 зелені
- [ ] після публікації нової версії фікстурою картка й оцінка використовують новий переклад, word status не змінюється (AC-42)
- [ ] чужі session чи картка — «не знайдено»
- [ ] бенчмарк: 200 послідовних відповідей на фікстурі з 1 000 слів learner — p95 обробки на сервері ≤ 500 ms
- [ ] `dotnet build`, тести й лінтер без нових попереджень
