---
id: T34
title: "Створити каркас Mini App, що його роздає бекенд"
layer: "ui"
deps: ["T01"]
acs: []
files_hint: ["ConstantLearning.MiniApp/", "ConstantLearning/wwwroot/", "ConstantLearning/Program.cs"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T34 — Створити каркас Mini App, що його роздає бекенд

**Шар:** `ui` · **Залежить від:** [T01](./T01-module-skeleton.md) · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 2

## Why

[ADR-0002](../adr/0002-build-mini-app-as-react-spa-served-by-aspnet-core.md); UI kit і data fetching — [sad §8](../sad.md#8-crosscutting-concepts).

## What

Vite + React + TypeScript, `@telegram-apps/telegram-ui` поверх `--tg-theme-*`, TanStack Query, клієнт і TS-типи з `openapi.yaml`, `mockTelegramEnv` для локальної розробки, роздача з `wwwroot` з fallback на `index.html`; механізм visual-regression з еталонами у світлій і темній темі Telegram.

Файли: `ConstantLearning.MiniApp/`, `ConstantLearning/wwwroot/`, `ConstantLearning/Program.cs`

## Definition of Done

- [ ] `npm run build` і `npm test` зелені
- [ ] SPA відкривається з бекенду за кореневим шляхом
- [ ] типи клієнта генеруються з контракту однією командою
- [ ] приклад visual-regression падає на навмисній зміні стилю й проходить з еталоном
- [ ] `npm run lint`, `npm run typecheck` і `npm test` зелені
- [ ] перевірки Mini App запускаються локально; додавання їх до CI не входить у цей епік

## Notes

Власної дизайн-системи в репозиторії немає — базовою є UI kit Telegram.
