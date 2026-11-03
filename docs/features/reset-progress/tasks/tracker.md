# Tracker — reset-progress

> Статус кожної задачі епіку. `implement-tasks` оновлює `done`, коли комітить задачу.
> Стани: `todo` · `in_progress` · `blocked` · `review` · `done`. Оцінка: S = 2 год, M = пів дня, L = день.

| # | Task | Layer | Owner | Estimate | Blocked by | Status |
|---|---|---|---|---|---|---|
| [T01](./T01-reset-flow-and-port.md) | Реалізувати потік скидання й порт обробників в Identity | app | Viacheslav Melnichenko | M | — | todo |
| [T02](./T02-learning-reset-handler.md) | Додати обробник скидання в Learning | app | Viacheslav Melnichenko | M | [T01](./T01-reset-flow-and-port.md), [T03](./T03-content-reset-handler.md) | todo |
| [T03](./T03-content-reset-handler.md) | Додати обробник скидання в Content: розірвати привʼязку reports до відповідей | app | Viacheslav Melnichenko | S | [T01](./T01-reset-flow-and-port.md) | todo |
| [T04](./T04-reset-endpoint.md) | Додати дію скидання прогресу в API й зареєструвати код помилки | ports | Viacheslav Melnichenko | S | [T01](./T01-reset-flow-and-port.md), [T02](./T02-learning-reset-handler.md), [T03](./T03-content-reset-handler.md) | todo |
| [T05](./T05-ui-reset-progress.md) | Додати кнопку, діалог підтвердження й обробку скинутого стану в Mini App | ui | Viacheslav Melnichenko | M | [T04](./T04-reset-endpoint.md) | todo |
| [T06](./T06-reset-integration-tests.md) | Дописати інтеграційні тести збою, одночасності й супутніх сутностей | tests | Viacheslav Melnichenko | M | [T04](./T04-reset-endpoint.md) | todo |
| [T07](./T07-reset-completeness-test.md) | Додати тест повноти скидання | tests | Viacheslav Melnichenko | S | [T02](./T02-learning-reset-handler.md), [T03](./T03-content-reset-handler.md) | todo |
| [T08](./T08-reset-telemetry-counter.md) | Додати агрегований лічильник скидань і метрику латентності | app | Viacheslav Melnichenko | S | [T04](./T04-reset-endpoint.md) | todo |
| [T09](./T09-reset-load-check.md) | Перевірити скидання на повному курсі й навантажувальному наборі | tests | Viacheslav Melnichenko | S | [T04](./T04-reset-endpoint.md) | todo |
| [T10](./T10-docs-update.md) | Оновити README, архітектурну карту й документацію батьківської фічі | docs | Viacheslav Melnichenko | S | [T05](./T05-ui-reset-progress.md), [T06](./T06-reset-integration-tests.md), [T08](./T08-reset-telemetry-counter.md) | todo |
