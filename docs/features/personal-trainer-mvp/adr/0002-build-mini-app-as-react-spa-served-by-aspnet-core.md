---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0002 — Build the Mini App as a React SPA served by ASP.NET Core

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

У репозиторії немає фронтенду. Mini App має відкривати «Сьогодні» за ≤ 1 500 ms p95 у WebView Telegram, мати стан заняття (картка, відповідь, різниця, наступна) і пошук на кожен символ (AC-30). Розробник — бекенд .NET.

## Decision drivers

- Latency p95 відкриття «Сьогодні» ≤ 1 500 ms (PRD §6)
- AC-30: результати після кожного введеного символу
- Екосистема Telegram Mini Apps (SDK, theme params, UI kit)
- Атомарний реліз UI й API, без CORS

## Considered options

1. **React 18.3 + TypeScript + Vite 5, роздача з `wwwroot` того самого ASP.NET Core** — Один origin, один образ, Node-етап у multi-stage Dockerfile.
2. **Blazor WebAssembly** — .NET на клієнті, але кілька MB рантайму й холодний старт 2–4 с у мобільному WebView.
3. **Razor Pages + htmx** — Найшвидший перший рендер, але стан заняття й відміна запитів пошуку все одно потребують ручного JS.

## Decision outcome

**Chosen:** Option 1. Бандл близько 150–200 KB gzip укладається в бюджет «Сьогодні», екосистема Telegram існує саме для React, а той самий origin прибирає CORS-preflight і розбіжність версій UI й API.

Уточнення 2026-11-01: компоненти Telegram UI використовують токени Everforest, світлу й темну теми та ручний вибір за [ADR-0013](./0013-use-everforest-with-manual-light-dark-themes.md). Це не змінює рішення про React SPA і спільний origin.

## Consequences

**Positive**
- Готові `@telegram-apps/sdk-react` і `@telegram-apps/telegram-ui`
- TS-типи генеруються з OpenAPI бекенду

**Negative**
- Другий тулчейн (Node 22, npm, Vitest) і дві мови для одного розробника
- Build context Docker і CI розширюються до кореня репозиторію; збірка довша на 1–2 хв
- Немає edge-CDN

**Neutral**
- Перехід на окремий статичний хостинг — близько дня, API лишається тим самим

## Links

- PRD: [[../PRD.md]]
- SAD: [[../sad.md]] §4
- Related ADR: [[0001-deliver-trainer-as-backend-service-plus-telegram-mini-app]], [[0005-authenticate-every-api-request-with-telegram-init-data]]
