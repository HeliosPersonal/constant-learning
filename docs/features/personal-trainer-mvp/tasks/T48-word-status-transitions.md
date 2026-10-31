---
id: T48
title: "Застосувати переходи word status і control repetition у відповіді"
layer: "app"
deps: ["T17"]
acs: ["AC-18", "AC-19", "AC-22", "AC-23"]
files_hint: ["ConstantLearning/Features/Learning/Sessions/Answer/Status/", "ConstantLearning.Tests/Learning/Sessions/Status/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T48 — Застосувати переходи word status і control repetition у відповіді

**Шар:** `app` · **Залежить від:** [T17](./T17-answer-item.md) · **AC:** AC-18, AC-19, AC-22, AC-23 · **Оцінка:** M · **Фаза:** 8

## Why

AC-18, AC-19, AC-22, AC-23 у [PRD](../PRD.md); гілки статусу в потоці «Оцінка відповіді й статус слова» в [sad §6](../sad.md).

## What

Перехід «На повторення» ↔ «Вже пригадую» за двома правильними відповідями в різних запланованих повтореннях, скидання після mistake, control repetition після «Вже знаю» — у транзакції відповіді.

Файли: `ConstantLearning/Features/Learning/Sessions/Answer/Status/`, `ConstantLearning.Tests/Learning/Sessions/Status/`

## Definition of Done

- [ ] інтеграційні тести AC-18, AC-19, AC-22, AC-23 зелені
- [ ] лічильник recalled words змінюється лише на цих переходах
- [ ] `dotnet build`, тести й лінтер без нових попереджень
