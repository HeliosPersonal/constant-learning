---
id: T38
title: "Зробити вправу пригадування, результат і оскарження"
layer: "ui"
deps: ["T37", "T17", "T24"]
acs: ["AC-12", "AC-13", "AC-14", "AC-15", "AC-16", "AC-17", "AC-55", "AC-56", "AC-57"]
files_hint: ["ConstantLearning.MiniApp/src/features/session/recall/"]
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T38 — Зробити вправу пригадування, результат і оскарження

**Шар:** `ui` · **Залежить від:** [T37](./T37-ui-session-shell.md), [T17](./T17-answer-item.md), [T24](./T24-answer-dispute.md) · **AC:** AC-12, AC-13, AC-14, AC-15, AC-16, AC-17, AC-55, AC-56, AC-57 · **Оцінка:** L · **Фаза:** 10

## Why

US-03 у [PRD](../PRD.md); Critical flow 1 і «Моя відповідь теж правильна» в [sad §6](../sad.md).

## What

Поле відповіді з «Не знаю», блокування порожньої відповіді, прохання змінити розкладку (AC-57), повідомлення про синонім (AC-56), показ diff і прикладу, кнопка «Моя відповідь теж правильна» лише коли `dispute_allowed`, повтор мутації з тим самим ключем картки.

Файли: `ConstantLearning.MiniApp/src/features/session/recall/`

## Definition of Done

- [ ] компонентні тести AC-12…AC-17, AC-55 зелені
- [ ] подвійне натискання надсилає відповідь один раз з погляду користувача
- [ ] еталони visual-regression у світлій і темній темі затверджено
- [ ] `npm run lint`, `npm run typecheck` і `npm test` зелені
