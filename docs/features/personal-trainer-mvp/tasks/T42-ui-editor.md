---
id: T42
title: "Зробити чергу content editor і список застосованих змін"
layer: "ui"
deps: ["T34", "T27", "T28"]
acs: ["AC-38", "AC-39", "AC-40", "AC-41", "AC-48", "AC-49", "AC-50"]
files_hint: ["ConstantLearning.MiniApp/src/features/editor/"]
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T42 — Зробити чергу content editor і список застосованих змін

**Шар:** `ui` · **Залежить від:** [T34](./T34-miniapp-skeleton.md), [T27](./T27-editor-queue.md), [T28](./T28-applied-changes-rollback.md) · **AC:** AC-38, AC-39, AC-40, AC-41, AC-48, AC-49, AC-50 · **Оцінка:** L · **Фаза:** 10

## Why

US-10, US-12 у [PRD](../PRD.md); потоки черги й відкату в [sad §6](../sad.md).

## What

Черга з псевдонімами й рішеннями, список застосованих змін з відкатом; вхід лише для ролі content editor; lazy-чанк.

Файли: `ConstantLearning.MiniApp/src/features/editor/`

## Definition of Done

- [ ] компонентні тести рішень і відкату зелені
- [ ] без ролі розділ недоступний (AC-41, AC-50)
- [ ] еталони visual-regression у світлій і темній темі затверджено
- [ ] `npm run lint`, `npm run typecheck` і `npm test` зелені
