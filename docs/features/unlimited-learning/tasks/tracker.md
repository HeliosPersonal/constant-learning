# Tracker — unlimited-learning

> Статус кожної задачі епіку. `implement-tasks` оновлює `done`, коли комітить задачу.
> Стани: `todo` · `in_progress` · `blocked` · `review` · `done`. Оцінка: S = 2 год, M = пів дня, L = день.

| # | Task | Layer | Owner | Estimate | Blocked by | Status |
|---|---|---|---|---|---|---|
| [T01](./T01-session-kind-expand.md) | Додати колонку kind у learning_sessions і писати її в нових session (expand) | migration | Viacheslav Melnichenko | S | — | done |
| [T02](./T02-session-kind-contract.md) | Заповнити kind для наявних session і зробити колонку NOT NULL (backfill + contract) | migration | Viacheslav Melnichenko | S | [T01](./T01-session-kind-expand.md) | done |
| [T03](./T03-scheduler-extra-practice.md) | Додати третій вид вправи ExtraPractice у планувальник і проєктор | domain | Viacheslav Melnichenko | M | — | done |
| [T04](./T04-plan-kind-filters.md) | Обмежити лічильники, нагадування й початок заняття видом plan | app | Viacheslav Melnichenko | M | [T01](./T01-session-kind-expand.md) | done |
| [T05](./T05-extra-availability-today.md) | Обчислити доступність extra-дій і додати extra_actions у відповідь екрана «Сьогодні» | app | Viacheslav Melnichenko | M | [T01](./T01-session-kind-expand.md), [T04](./T04-plan-kind-filters.md) | done |
| [T06](./T06-start-extra-word.md) | Реалізувати старт «Ще одне слово»: session extra_word, добір слова, ідемпотентність, захист від зловживань | app | Viacheslav Melnichenko | L | [T01](./T01-session-kind-expand.md), [T04](./T04-plan-kind-filters.md) | done |
| [T07](./T07-start-extra-practice.md) | Реалізувати старт «Потренувати вивчені»: відбір до 20 слів і session extra_practice | app | Viacheslav Melnichenko | M | [T03](./T03-scheduler-extra-practice.md), [T05](./T05-extra-availability-today.md) | done |
| [T08](./T08-answer-pipeline-extra.md) | Підтримати extra-картки у відповіді, межі доби й походженні відповіді | app | Viacheslav Melnichenko | L | [T03](./T03-scheduler-extra-practice.md), [T06](./T06-start-extra-word.md), [T07](./T07-start-extra-practice.md) | done |
| [T09](./T09-extra-endpoints-contract.md) | Додати дії extra в API, зареєструвати коди помилок і внести зміни в контракт батьківської фічі | ports | Viacheslav Melnichenko | S | [T05](./T05-extra-availability-today.md), [T06](./T06-start-extra-word.md), [T07](./T07-start-extra-practice.md) | done |
| [T10](./T10-ui-today-and-session.md) | Показати дії extra на екрані «Сьогодні» й підтримати extra-заняття в екрані заняття | ui | Viacheslav Melnichenko | M | [T09](./T09-extra-endpoints-contract.md) | done |
| [T11](./T11-extra-integration-tests.md) | Дописати інтеграційні тести крайових випадків і відтворення журналу | tests | Viacheslav Melnichenko | M | [T08](./T08-answer-pipeline-extra.md), [T09](./T09-extra-endpoints-contract.md) | done |
| [T12](./T12-extra-load-check.md) | Перевірити швидкість старту extra й практики на навантажувальному наборі | tests | Viacheslav Melnichenko | S | [T09](./T09-extra-endpoints-contract.md) | done |
| [T13](./T13-docs-update.md) | Оновити README, CONTEXT і документацію батьківської фічі | docs | Viacheslav Melnichenko | S | [T10](./T10-ui-today-and-session.md), [T11](./T11-extra-integration-tests.md) | done |
