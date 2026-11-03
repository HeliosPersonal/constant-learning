---
id: T08
title: "Підтримати extra-картки у відповіді, межі доби й походженні відповіді"
layer: "app"
deps: ["[T03](./T03-scheduler-extra-practice.md)", "[T06](./T06-start-extra-word.md)", "[T07](./T07-start-extra-practice.md)"]
acs: ["AC-07", "AC-12", "AC-13", "AC-14", "AC-15", "AC-20"]
files_hint: ["ConstantLearning/Features/Learning/Sessions/Answer/", "ConstantLearning/Features/Learning/Sessions/DayBoundary/", "ConstantLearning/Features/Learning/Sessions/Current/", "ConstantLearning/Features/Learning/Disputes/", "ConstantLearning.Tests/Learning/Sessions/"]
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T08 — Підтримати extra-картки у відповіді, межі доби й походженні відповіді

**Шар:** `app` · **Залежить від:** [T03](./T03-scheduler-extra-practice.md), [T06](./T06-start-extra-word.md), [T07](./T07-start-extra-practice.md) · **AC:** [AC-07](../PRD.md#ac-07---domain-invariant), [AC-12](../PRD.md#ac-12---domain-invariant), [AC-13](../PRD.md#ac-13---domain-invariant), [AC-14](../PRD.md#ac-14---domain-invariant), [AC-15](../PRD.md#ac-15---domain-invariant), [AC-20](../PRD.md#ac-20---domain-invariant) · **Оцінка:** L

## Why

[ADR-0002](../adr/0002-model-extra-practice-as-a-third-exercise-kind-in-the-scheduler.md), [SAD §8](../sad.md#8-crosscutting-concepts): одна відповідь на картку, закриття на межі доби, походження в exercise_kind.

## What

Дозволити картки `extra_new_word_check` і `extra_practice`: пом'якшити перевірку «слово до повторення» для практики; мапити вид картки на вид вправи планувальника; писати `exercise_kind` із походженням. Закриття extra_word і extra_practice на межі доби: слово отримане, відповіді лишаються за своєю добою, невідповіджені картки не рахуються. Оскарження помилки практики відновлює стан і виключає слово до кінця доби.

Файли: `ConstantLearning/Features/Learning/Sessions/Answer/`, `ConstantLearning/Features/Learning/Sessions/DayBoundary/`, `ConstantLearning/Features/Learning/Sessions/Current/`, `ConstantLearning/Features/Learning/Disputes/`, `ConstantLearning.Tests/Learning/Sessions/`

## Definition of Done

- [ ] інтеграційні тести AC-07, AC-12, AC-13, AC-14, AC-15, AC-20 зелені
- [ ] повторне надсилання тієї самої відповіді зараховується один раз
- [ ] відповідь у журналі має exercise_kind extra_new_word_check або extra_practice
- [ ] `dotnet build` і тести без нових попереджень
