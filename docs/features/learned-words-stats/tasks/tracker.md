# Tracker — learned-words-stats

> Статус кожної задачі епіку. `implement-tasks` оновлює `done`, коли комітить задачу.
> Стани: `todo` · `in_progress` · `blocked` · `review` · `done`. Оцінка: S = 2 год, M = пів дня, L = день.

| # | Task | Layer | Owner | Estimate | Blocked by | Status |
|---|---|---|---|---|---|---|
| [T01](./T01-stats-contract-and-errors.md) | Додати ендпоінти /stats/* у контракт батьківської фічі й зареєструвати коди помилок stats.* | ports | Viacheslav Melnichenko | S | — | done |
| [T02](./T02-stats-service-counting.md) | Реалізувати LearnedWordsStatsService: лічильники наживо з журналу з анти-join оскаржень і показ з опублікованої версії | app | Viacheslav Melnichenko | L | — | done |
| [T03](./T03-stats-controller-and-request.md) | Додати StatsController і LearnedWordsStatsRequest: розбір запиту, курсор за entry_id, валідація вкладки/сортування/пошуку й телеметрія | app | Viacheslav Melnichenko | M | [T01](./T01-stats-contract-and-errors.md), [T02](./T02-stats-service-counting.md) | done |
| [T04](./T04-ui-my-words-screen.md) | Побудувати екран «Мої слова» в Mini App: вкладки, пошук, сортування, лічильники й картка з розбивкою помилок | ui | Viacheslav Melnichenko | M | [T03](./T03-stats-controller-and-request.md) | done |
| [T05](./T05-stats-integration-tests.md) | Дописати інтеграційні тести трьох ендпоінтів: семантика лічильників, вкладки, пагінація, авторизація й очищення після скидання | tests | Viacheslav Melnichenko | M | [T02](./T02-stats-service-counting.md), [T03](./T03-stats-controller-and-request.md) | done |
| [T06](./T06-docs-update.md) | Оновити README, CONTEXT і документацію батьківської фічі | docs | Viacheslav Melnichenko | S | [T04](./T04-ui-my-words-screen.md), [T05](./T05-stats-integration-tests.md) | done |
