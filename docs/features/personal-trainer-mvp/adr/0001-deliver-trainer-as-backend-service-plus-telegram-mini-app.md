---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0001 — Deliver the trainer as a backend service plus a Telegram Mini App

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

[PRD §1](../PRD.md#1-context) обирає персональний trainer — застосунок усередині Telegram, де learner проходить заняття, а бот нагадує. Потрібно визначити, які поверхні будуємо і де виконуються фонові задачі: нагадування (AC-26), підсумки content reports (AC-37), дайджест редактора (AC-43) і звірка (AC-35).

## Decision drivers

- Пілот ~50 learner, ≥ 100 одночасних на одному екземплярі ([PRD §6](../PRD.md#6-non-functional-requirements))
- ≥ 99% нагадувань у межах 2 хв ([PRD §6](../PRD.md#6-non-functional-requirements))
- Один розробник, один Deployment з `replicas: 1` (§2)
- Quartz у процесі вже є прецедентом (`architecture-map.md` §Conventions)

## Considered options

1. **`[backend-service, web-frontend]`, фонові задачі в процесі бекенду** — Mini App як SPA + ASP.NET Core API, Quartz-тики в тому самому процесі.
2. **`[backend-service, web-frontend, worker]`** — Окремий Deployment з того самого образу лише для фонових задач.
3. **`[backend-service, web-frontend]` + зовнішній k8s CronJob** — CronJob щохвилини викликає внутрішній endpoint.

## Decision outcome

**Chosen:** Option 1. Тика кожні 30 секунд у процесі досить для цілі 2 хв, а гарантію «не більше одного на дату» дає унікальний ключ у БД ([ADR-0007](0007-dispatch-notifications-through-postgres-dispatch-table.md)), а не топологія; окремий worker виправданий лише з другою реплікою.

## Consequences

**Positive**
- Один образ і один деплой
- Фонові задачі мають доступ до того самого `AppDbContext` і доменних сервісів

**Negative**
- Рестарт API зупиняє і розсилку, і UI на час рестарту
- Розсилка ділить CPU й пул з'єднань із пошуком

**Neutral**
- Виділення worker пізніше — близько дня роботи, бо тики ізольовані в модулі `Notifications`

## Links

- PRD: [PRD](../PRD.md)
- SAD: [SAD](../sad.md) §4
- Related ADR: [ADR-0002](0002-build-mini-app-as-react-spa-served-by-aspnet-core.md), [ADR-0007](0007-dispatch-notifications-through-postgres-dispatch-table.md)
