# Tracker — reset-progress

> Статус кожної задачі епіку. `implement-tasks` оновлює `done`, коли комітить задачу.
> Стани: `todo` · `in_progress` · `blocked` · `review` · `done`. Оцінка: S = 2 год, M = пів дня, L = день.

| # | Task | Layer | Owner | Estimate | Blocked by | Status |
|---|---|---|---|---|---|---|
| [T01](./T01-reset-flow-and-port.md) | Реалізувати потік скидання й порт обробників в Identity | app | Viacheslav Melnichenko | M | — | done |
| [T02](./T02-learning-reset-handler.md) | Додати обробник скидання в Learning | app | Viacheslav Melnichenko | M | [T01](./T01-reset-flow-and-port.md), [T03](./T03-content-reset-handler.md) | done |
| [T03](./T03-content-reset-handler.md) | Додати обробник скидання в Content: розірвати привʼязку reports до відповідей | app | Viacheslav Melnichenko | S | [T01](./T01-reset-flow-and-port.md) | done |
| [T04](./T04-reset-endpoint.md) | Додати дію скидання прогресу в API й зареєструвати код помилки | ports | Viacheslav Melnichenko | S | [T01](./T01-reset-flow-and-port.md), [T02](./T02-learning-reset-handler.md), [T03](./T03-content-reset-handler.md) | done |
| [T05](./T05-ui-reset-progress.md) | Додати кнопку, діалог підтвердження й обробку скинутого стану в Mini App | ui | Viacheslav Melnichenko | M | [T04](./T04-reset-endpoint.md) | done |
| [T06](./T06-reset-integration-tests.md) | Дописати інтеграційні тести збою, одночасності й супутніх сутностей | tests | Viacheslav Melnichenko | M | [T04](./T04-reset-endpoint.md) | done |
| [T07](./T07-reset-completeness-test.md) | Додати тест повноти скидання | tests | Viacheslav Melnichenko | S | [T02](./T02-learning-reset-handler.md), [T03](./T03-content-reset-handler.md) | done |
| [T08](./T08-reset-telemetry-counter.md) | Додати агрегований лічильник скидань і метрику латентності | app | Viacheslav Melnichenko | S | [T04](./T04-reset-endpoint.md) | done |
| [T09](./T09-reset-load-check.md) | Перевірити скидання на повному курсі й навантажувальному наборі | tests | Viacheslav Melnichenko | S | [T04](./T04-reset-endpoint.md) | review |
| [T10](./T10-docs-update.md) | Оновити README, архітектурну карту й документацію батьківської фічі | docs | Viacheslav Melnichenko | S | [T05](./T05-ui-reset-progress.md), [T06](./T06-reset-integration-tests.md), [T08](./T08-reset-telemetry-counter.md) | done |

> **CI/локальний стенд (не запускається в цьому середовищі — node/npm, python і Docker недоступні локально):**
> - T05: `npm run generate:api`, `npm run test` (vitest), `tsc`, Playwright e2e — схему `schema.d.ts` відредаговано вручну відповідно до контракту v0.2.0, тести дописано; зелений статус підтверджує CI.
> - T09: сценарій [`reset.js`](../../../../tests/load/reset.js) і seed-когорта в `tests/load/Program.cs` готові; **p95-звіт очікує прогону на локальному навантажувальному стенді** (Docker + k6), як і для T49 — тому статус `review`, а не `done`.
> - T10: `python tools/docs/check_links.py` — перевірка посилань виконується в CI.
