---
id: T34
title: "Створити каркас Mini App, що його роздає бекенд"
layer: "ui"
deps: ["T01"]
acs: ["AC-58"]
files_hint: ["ConstantLearning.MiniApp/", "ConstantLearning/wwwroot/", "ConstantLearning/Program.cs"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T34 — Створити каркас Mini App, що його роздає бекенд

**Шар:** `ui` · **Залежить від:** [T01](./T01-module-skeleton.md) · **AC:** AC-58 · **Оцінка:** M · **Фаза:** 2

## Why

[ADR-0002](../adr/0002-build-mini-app-as-react-spa-served-by-aspnet-core.md); UI kit і data fetching — [sad §8](../sad.md#8-crosscutting-concepts).

## What

Vite + React + TypeScript, `@telegram-apps/telegram-ui` із семантичними токенами Everforest, TanStack Query, клієнт і TS-типи з `openapi.yaml`, `mockTelegramEnv` для локальної розробки, роздача з `wwwroot` з fallback на `index.html`; механізм visual-regression з еталонами у світлій і темній темі Telegram.

Файли: `ConstantLearning.MiniApp/`, `ConstantLearning/wwwroot/`, `ConstantLearning/Program.cs`

## Definition of Done

- [ ] локальні перевірки AC-58 підтверджують раннє застосування теми, ручний пріоритет, повторне відкриття, fallback сховища та контраст токенів; modal/portal використовують ту саму тему

- [ ] екран і його стани використовують токени [Everforest](../design-system.md) та коректно відображаються у світлій і темній темах

- [ ] `npm run build` і `npm test` зелені
- [ ] SPA відкривається з бекенду за кореневим шляхом
- [ ] типи клієнта генеруються з контракту однією командою
- [ ] приклад visual-regression падає на навмисній зміні стилю й проходить з еталоном
- [ ] `npm run lint`, `npm run typecheck` і `npm test` зелені
- [ ] перевірки Mini App запускаються локально; додавання їх до CI не входить у цей епік

## Notes

Дизайн-система — [Everforest](../design-system.md), токени — [theme-tokens.json](../design/theme-tokens.json). UI kit Telegram залишається базою компонентів (ADR-0013).

- Провайдер теми завантажує валідний локальний вибір до першого видимого рендера; за його відсутності використовує Telegram, ОС або світлу схему. Обробляє недоступне сховище, themeChanged та portal-компоненти.
- Локальні перевірки AC-58: початкова тема, повторне відкриття, помилка сховища, ручний пріоритет, контраст токенів.
