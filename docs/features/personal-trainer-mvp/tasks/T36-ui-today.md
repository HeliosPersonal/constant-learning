---
id: T36
title: "Зробити екран «Сьогодні»"
layer: "ui"
deps: ["T34", "T19", "T33"]
acs: ["AC-06", "AC-10", "AC-28"]
files_hint: ["ConstantLearning.MiniApp/src/features/today/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T36 — Зробити екран «Сьогодні»

**Шар:** `ui` · **Залежить від:** [T34](./T34-miniapp-skeleton.md), [T19](./T19-today-api.md), [T33](./T33-client-telemetry.md) · **AC:** AC-06, AC-10, AC-28 · **Оцінка:** M · **Фаза:** 8

## Why

US-07 у [PRD](../PRD.md); правило «Визначення поясу» в [PRD §5](../PRD.md#5-acceptance-criteria).

## What

Лічильники прогресу, кнопка заняття, стан курсу, пропозиція перейти на пояс пристрою без автоматичної зміни, телеметрія часу готовності.

Файли: `ConstantLearning.MiniApp/src/features/today/`

## Definition of Done

- [ ] екран і його стани використовують токени [Everforest](../design-system.md) та коректно відображаються у світлій і темній темах

- [ ] компонентні тести AC-28 і пропозиції поясу зелені
- [ ] телеметрія `today_ready_ms` надсилається один раз
- [ ] еталони visual-regression у світлій і темній темі затверджено
- [ ] `npm run lint`, `npm run typecheck` і `npm test` зелені
