---
id: T40
title: "Зробити форму content report"
layer: "ui"
deps: ["[T39](T39-ui-dictionary.md)", "[T23](T23-content-report-api.md)"]
acs: ["AC-34", "AC-36", "AC-46", "AC-51"]
files_hint: ["ConstantLearning.MiniApp/src/features/dictionary/report/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T40 — Зробити форму content report

**Шар:** `ui` · **Залежить від:** [T39](./T39-ui-dictionary.md), [T23](./T23-content-report-api.md) · **AC:** AC-34, AC-36, AC-46, AC-51 · **Оцінка:** M · **Фаза:** 10

## Why

US-09 у [PRD](../PRD.md); потік «Content report і автоматична звірка» в [sad §6](../sad.md).

## What

Вибір поля, запропоноване значення, коментар як звичайний текст, «Дякуємо, передали на перевірку», пояснення ліміту зі збереженням введеного.

Файли: `ConstantLearning.MiniApp/src/features/dictionary/report/`

## Definition of Done

- [ ] екран і його стани використовують токени [Everforest](../design-system.md) та коректно відображаються у світлій і темній темах

- [ ] компонентні тести AC-34, AC-46, AC-51 зелені
- [ ] `npm run lint`, `npm run typecheck` і `npm test` зелені
