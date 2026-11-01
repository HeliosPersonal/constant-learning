---
id: T35
title: "Зробити екран онбордингу"
layer: "ui"
deps: ["T34", "T13"]
acs: ["AC-01", "AC-02", "AC-27", "AC-53"]
files_hint: ["ConstantLearning.MiniApp/src/features/onboarding/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T35 — Зробити екран онбордингу

**Шар:** `ui` · **Залежить від:** [T34](./T34-miniapp-skeleton.md), [T13](./T13-onboarding-api.md) · **AC:** AC-01, AC-02, AC-27, AC-53 · **Оцінка:** M · **Фаза:** 5

## Why

US-01 у [PRD](../PRD.md); потік «Онбординг learner» у [sad §6](../sad.md).

## What

Вибір ліміту 3/5/10, необов'язкове нагадування з днями тижня, визначення поясу пристрою; повторний онбординг веде на «Сьогодні». Компоненти — з UI kit Telegram.

Файли: `ConstantLearning.MiniApp/src/features/onboarding/`

## Definition of Done

- [ ] екран і його стани використовують токени [Everforest](../design-system.md) та коректно відображаються у світлій і темній темах

- [ ] до завершення онбордингу learner бачить повідомлення про обробку персональних даних і контакт власника проєкту для запиту на видалення
- [ ] компонентні тести: збереження без днів показує пояснення (AC-02)
- [ ] без поясу нагадування просить вибрати пояс (AC-27)
- [ ] `npm run lint`, `npm run typecheck` і `npm test` зелені
