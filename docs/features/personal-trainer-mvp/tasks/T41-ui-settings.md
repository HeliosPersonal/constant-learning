---
id: T41
title: "Зробити екран налаштувань"
layer: "ui"
deps: ["T34", "T14"]
acs: ["AC-27", "AC-44", "AC-45"]
files_hint: ["ConstantLearning.MiniApp/src/features/settings/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T41 — Зробити екран налаштувань

**Шар:** `ui` · **Залежить від:** [T34](./T34-miniapp-skeleton.md), [T14](./T14-settings-api.md) · **AC:** AC-27, AC-44, AC-45 · **Оцінка:** M · **Фаза:** 6

## Why

US-11 у [PRD](../PRD.md); потік «Налаштування темпу, поясу й нагадувань» у [sad §6](../sad.md).

## What

Ліміт 3/5/10 з поясненням «з наступної доби», вибір поясу, нагадування з днями тижня; lazy-чанк.

Файли: `ConstantLearning.MiniApp/src/features/settings/`

## Definition of Done

- [ ] компонентні тести AC-44, AC-45 зелені
- [ ] `npm run lint`, `npm run typecheck` і `npm test` зелені
