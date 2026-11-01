---
id: T37
title: "Зробити каркас заняття, картку нового слова й підсумок"
layer: "ui"
deps: ["[T36](T36-ui-today.md)", "[T18](T18-new-word-card.md)"]
acs: ["AC-03", "AC-11", "AC-20", "AC-21", "AC-24", "AC-52"]
files_hint: ["ConstantLearning.MiniApp/src/features/session/shell/", "ConstantLearning.MiniApp/src/features/session/new-word/"]
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T37 — Зробити каркас заняття, картку нового слова й підсумок

**Шар:** `ui` · **Залежить від:** [T36](./T36-ui-today.md), [T18](./T18-new-word-card.md) · **AC:** AC-03, AC-11, AC-20, AC-21, AC-24, AC-52 · **Оцінка:** L · **Фаза:** 9

## Why

US-02, US-04, US-05 у [PRD](../PRD.md); потоки «Картка нового слова» й «Продовження session» у [sad §6](../sad.md).

## What

Перехід між картками, картка нового слова з «Далі» і «Вже знаю», продовження перерваного заняття, підсумок, обробка `session.expired` з поясненням і переходом на «Сьогодні».

Файли: `ConstantLearning.MiniApp/src/features/session/shell/`, `ConstantLearning.MiniApp/src/features/session/new-word/`

## Definition of Done

- [ ] екран і його стани використовують токени [Everforest](../design-system.md) та коректно відображаються у світлій і темній темах

- [ ] компонентні тести AC-11, AC-20, AC-24, AC-52 зелені
- [ ] еталони visual-regression у світлій і темній темі затверджено
- [ ] `npm run lint`, `npm run typecheck` і `npm test` зелені
