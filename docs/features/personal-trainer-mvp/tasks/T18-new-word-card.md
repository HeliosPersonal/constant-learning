---
id: T18
title: "Реалізувати картку нового слова, «Далі» і «Вже знаю»"
layer: "app"
deps: ["[T17](T17-answer-item.md)", "[T10](T10-review-scheduler.md)"]
acs: ["AC-11", "AC-20", "AC-21", "AC-52"]
files_hint: ["ConstantLearning/Features/Learning/Sessions/NewWords/", "ConstantLearning.Tests/Learning/Sessions/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T18 — Реалізувати картку нового слова, «Далі» і «Вже знаю»

**Шар:** `app` · **Залежить від:** [T17](./T17-answer-item.md), [T10](./T10-review-scheduler.md) · **AC:** AC-11, AC-20, AC-21, AC-52 · **Оцінка:** M · **Фаза:** 8

## Why

US-02, US-04 у [PRD](../PRD.md); потік «Картка нового слова й «Вже знаю»» у [sad §6](../sad.md); `acknowledgeNewWordCard`, `markAlreadyKnown`.

## What

Фіксація першого показу, перевірка нового слова після карток, «Вже знаю» з control repetition через 7 діб і заміною до 10 разів за добу; понад 10 — без заміни, слово рахується до денного ліміту.

Файли: `ConstantLearning/Features/Learning/Sessions/NewWords/`, `ConstantLearning.Tests/Learning/Sessions/`

## Definition of Done

- [ ] інтеграційні тести AC-11, AC-20, AC-21 зелені
- [ ] інтеграційні тести AC-52 для «Далі» і «Вже знаю» зелені
- [ ] чужа картка — «не знайдено»
- [ ] повтор «Далі» і «Вже знаю» безпечний
- [ ] `dotnet build`, тести й лінтер без нових попереджень
