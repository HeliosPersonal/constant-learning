---
id: T24
title: "Реалізувати «Моя відповідь теж правильна»"
layer: "app"
deps: ["[T17](T17-answer-item.md)", "[T23](T23-content-report-api.md)"]
acs: ["AC-17", "AC-36", "AC-47", "AC-55"]
files_hint: ["ConstantLearning/Features/Learning/Disputes/", "ConstantLearning.Tests/Learning/Disputes/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T24 — Реалізувати «Моя відповідь теж правильна»

**Шар:** `app` · **Залежить від:** [T17](./T17-answer-item.md), [T23](./T23-content-report-api.md) · **AC:** AC-17, AC-36, AC-47, AC-55 · **Оцінка:** M · **Фаза:** 8

## Why

AC-17, AC-47, AC-55 у [PRD](../PRD.md); потік «Моя відповідь теж правильна» в [sad §6](../sad.md); `disputeAnswer`.

## What

Нейтралізація mistake зі знімка стану, content report `answer_dispute` поза лімітом (один на відповідь), окремий випадок перевірки нового слова; вікно доступності — поки показано результат картки, до переходу на наступну або кінця доби (AC-17).

Файли: `ConstantLearning/Features/Learning/Disputes/`, `ConstantLearning.Tests/Learning/Disputes/`

## Definition of Done

- [ ] інтеграційні тести AC-17, AC-47, AC-55 зелені
- [ ] report оскарження йде в чергу редактора без звірки (AC-36)
- [ ] повторний виклик повертає той самий результат
- [ ] дія після закриття вікна повертає `answer.dispute_not_allowed`
- [ ] чужа відповідь — «не знайдено»
- [ ] `dotnet build`, тести й лінтер без нових попереджень
