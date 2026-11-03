---
id: T07
title: "Реалізувати старт «Потренувати вивчені»: відбір до 20 слів і session extra_practice"
layer: "app"
deps: ["[T03](./T03-scheduler-extra-practice.md)", "[T05](./T05-extra-availability-today.md)"]
acs: ["AC-08", "AC-09", "AC-10", "AC-11"]
files_hint: ["ConstantLearning/Features/Learning/Sessions/Extra/", "ConstantLearning.Tests/Learning/Sessions/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T07 — Реалізувати старт «Потренувати вивчені»: відбір до 20 слів і session extra_practice

**Шар:** `app` · **Залежить від:** [T03](./T03-scheduler-extra-practice.md), [T05](./T05-extra-availability-today.md) · **AC:** [AC-08](../PRD.md#ac-08---happy-path), [AC-09](../PRD.md#ac-09---domain-invariant), [AC-10](../PRD.md#ac-10---error), [AC-11](../PRD.md#ac-11---cross-context) · **Оцінка:** M

## Why

[PRD US-02](../PRD.md#4-user-stories), [SAD §6](../sad.md#відбір-слів-для-практики).

## What

Старт збирає до 20 слів у порядку найближчої дати наступного повторення за правилом придатності з T05, створює session `extra_practice` з картками на позиціях 1–20. Без придатних слів повертає помилку з причиною. Слова без `approved` виключаються, вже показану картку пропущено без зарахування.

Файли: `ConstantLearning/Features/Learning/Sessions/Extra/`, `ConstantLearning.Tests/Learning/Sessions/`

## Definition of Done

- [ ] інтеграційні тести AC-08, AC-09, AC-10, AC-11 зелені
- [ ] практика не створює completed session
- [ ] слово з відповіддю сьогодні не потрапляє в повторну практику того ж дня
- [ ] `dotnet build` і тести без нових попереджень
