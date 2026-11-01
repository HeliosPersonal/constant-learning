# Вибір Agency-агентів і моделей для реалізації

Оновлено: 2026-11-01. Це план призначення виконавців для 49 задач, а не результат їх виконання. Джерело призначень — [tasks/tasks.json](./tasks/tasks.json): `execution_profiles` описує Agency-ролі, а `execution` кожної задачі задає профіль, модель, рівень міркування та обґрунтування. Статуси виконання залишаються в [tracker.md](./tasks/tracker.md).

## Порівняння з Agency

Порівняно Markdown задач і встановлені `SKILL.md` відповідних ролей у каталозі `agency-*`. Agency дає спеціалізацію та робочі правила; це не гарантія якості конкретної моделі або знання .NET.

| Потрібна роль | Відповідник Agency | Рішення та межі застосування |
|---|---|---|
| Виконавець .NET/EF/PostgreSQL | `Backend Architect` / `agency-backend-architect` | Основний виконавець backend, зокрема [T01](tasks/T01-module-skeleton.md) і [T02](tasks/T02-test-harness.md). Скіл охоплює API, дані й реалізацію. Застосовувати в погодженому моноліті, без нової інфраструктури. |
| Звичайні EF-міграції | `Database Reliability Engineer` | Для [T03](tasks/T03-migrate-identity.md)–[T07](tasks/T07-migrate-search-jobs-dispatches.md) надто широкий операційний профіль: основна робота — мапінг готового DDL у EF. Залишено `Backend Architect`. |
| Відновлення після видалення таблиць | `Database Reliability Engineer` / `agency-database-reliability-engineer` | Призначено [T44](tasks/T44-migrate-drop-group-model.md): backup/restore є прямою частиною DoD. HA, failover і PgBouncer зі скіла не входять у задачу. |
| Telegram-автентифікація та авторизація | `Identity & Access Engineer` / `agency-identity-access-engineer` | Призначено [T12](tasks/T12-initdata-auth.md). Зберігається HMAC initData за [ADR-0005](adr/0005-authenticate-every-api-request-with-telegram-init-data.md); OIDC/PKCE зі скіла не замінює протокол. |
| Пошук і SQL | `Database Optimizer` / `agency-database-optimizer` | Допоміжний профіль для [T21](tasks/T21-search-forms.md)/[T22](tasks/T22-dictionary-search-api.md) за потреби. Власником API й інтеграції публікації залишається backend-виконавець. |
| Публічна API-платформа | `API Platform Engineer` / `agency-api-platform-engineer` | Не призначено: портал, зовнішні SDK та політика deprecation ширші за внутрішній API Mini App. Контрактний harness [T02](tasks/T02-test-harness.md) виконує backend-агент. |
| Проєктування архітектури | `Software Architect` / `agency-software-architect` | Не призначено окремим виконавцем: архітектуру вже визначено, [T01](tasks/T01-module-skeleton.md) потребує імплементації .NET. |
| Інтеграційний harness .NET | `API Tester` / `agency-api-tester` | Не призначено власником [T02](tasks/T02-test-harness.md): API-перевірки — лише частина Testcontainers/FakeClock/harness. Типові пороги й додаткові аудити зі скіла не додаються. |
| React Mini App | `Frontend Developer` / `agency-frontend-developer` | Прямий збіг для [T34](tasks/T34-miniapp-skeleton.md)–[T42](tasks/T42-ui-editor.md). Telegram UI з токенами затвердженої дизайн-системи Everforest, локальні перевірки та готові API; новий frontend CI не додається. |
| Локальне видалення legacy | `Minimal Change Engineer` / `agency-minimal-change-engineer` | Призначено [T43](tasks/T43-retire-group-code.md). Правило скіла про кількість файлів не обмежує прямо потрібний обсяг [T43](tasks/T43-retire-group-code.md). |
| Docker/k8s та експлуатація | `DevOps Automator` / `agency-devops-automator` | Призначено [T45](tasks/T45-deployment-fixes.md). Межі: один екземпляр, базовий моніторинг і погоджені копії; без додаткового autoscaling/CD. |
| Playwright | `Test Automation Engineer` / `agency-test-automation-engineer` | Призначено [T46](tasks/T46-e2e-critical-flows.md). Локальні E2E й один Telegram smoke; типові вимоги CI та десяти повторів зі скіла не застосовуються. |
| Навантаження та p95 | `Performance Benchmarker` / `agency-performance-benchmarker` | Призначено [T49](tasks/T49-load-test.md). Пороги й сценарії визначені PRD/[T49](tasks/T49-load-test.md); типові stress/endurance-плани скіла не додаються. |
| Перевірка коду | `Code Reviewer` / `agency-code-reviewer` | Придатний за окремої потреби у перевірці diff. Обов'язковий другий агент для кожної задачі та окремий security review не вводяться. |

Agency `UI Designer` / `agency-ui-designer` додано як необов'язкову консультацію [T34](tasks/T34-miniapp-skeleton.md)/[T41](tasks/T41-ui-settings.md): токени, стани компонентів і контраст затвердженої Everforest. Імплементацію цих задач виконує Frontend Developer.

## Моделі

За замовчуванням використовується `gpt-6.1-sol` із `high`. Для 16 задач із часовими межами, складною історією стану, конкурентними транзакціями, доставкою чи руйнівною міграцією призначено `gpt-6-astra` із `high`. Це інженерна оцінка ризику задач, не результат порівняльного запуску моделей на репозиторії.

Luna та Terra не призначені основними виконавцями: у цьому плані навіть UI-задачі інтегруються з авторизацією, станом session та контрактами. Детальне обґрунтування кожного призначення — `execution.rationale`.

## Як координатор застосовує позначки

1. На початку реалізації прочитати цей документ, `tasks.json`, `tracker.md` і актуальні інструкції проєкту. Ці поля є розширенням плану; вони не доводять, що стороння команда `implement-tasks` уже підтримує маршрутизацію. Координатор має прочитати їх явно. Окремий програмний диспетчер цим оновленням не створено.
2. Вибрати задачу з виконаними `deps`. Міграції виконувати послідовно. Для інших задач порівняти `files_hint`, включно з перетинами батьківських каталогів і вкладених шляхів, та уточнити фактичні файли. Спільні `Program.cs`, DbContext, snapshot, контракти й тестові фікстури не редагувати паралельно. Однаковий модуль сам по собі не означає незалежності.
3. За поточного ліміту чотирьох агентів залишити один слот координатору й запускати щонайбільше три виконавці. Використовувати менше, якщо актуальні ліміти або перетини файлів цього вимагають.
4. Розв'язати `execution.profile` через `execution_profiles`, знайти встановлений скіл за назвою та прочитати його. Використовувати доступний `agent_type`, `model` і `reasoning_effort` задачі. Для явного вибору моделі запускати субагента з `fork_turns: none`: успадкування всієї розмови не підтримує перевизначення моделі.
5. Передати виконавцю AGENTS.md, Markdown задачі, потрібні розділи PRD/SAD/ADR, контракти та результати залежностей, `scope` профілю й точний перелік файлів у його відповідальності. Явно повідомити, що інші агенти працюють поруч і їхні зміни не можна скасовувати. Для розширення набору файлів спершу повторно перевірити перетини.
6. Якщо роль недоступна, але скіл доступний, використати `worker` із явно прочитаним Agency-скілом. Якщо скіла немає, не називати запуск Agency-виконанням. Недоступну модель не підміняти мовчки: повідомити про обмеження й узгодити доступний варіант.
7. `consult_profiles` — необов'язкова вузька консультація в межах тих самих лімітів, а не автоматичний запуск другого виконавця. За складної невирішеної проблеми Sol можна передати задачу Astra з конкретним описом невдалої спроби й результатами перевірок. Самої тривалості роботи недостатньо для ескалації.
8. Виконати перевірки з DoD задачі, зберегти фактичні результати й передати diff координатору. Позначка моделі, успішне завершення агента або його впевненість не є підставою для `done`. Вимоги до комітів і статусів визначають чинні правила реалізації. Призначення агента на [T44](tasks/T44-migrate-drop-group-model.md)/[T45](tasks/T45-deployment-fixes.md) не означає дозволу виконувати реліз чи руйнівну операцію в робочій БД.

Рішення користувача й погоджені PRD/ADR/DoD визначають обсяг. Загальні правила Agency не відновлюють скасований security review, не додають frontend CI і не змінюють один Telegram smoke на нову програму ручного тестування. Backend CI та навантажувальні перевірки [T49](tasks/T49-load-test.md) зберігають власні вимоги.

## Призначення задач

Таблиця нижче — огляд `execution` із `tasks.json`. Під час зміни призначень оновлювати обидва представлення; машинним джерелом залишається JSON.

| Задача | Agency-агент | Модель | Міркування |
|---|---|---|---|
| [T01](./tasks/T01-module-skeleton.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T02](./tasks/T02-test-harness.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T03](./tasks/T03-migrate-identity.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T04](./tasks/T04-migrate-content-core.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T05](./tasks/T05-migrate-learning.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T06](./tasks/T06-migrate-reports-publication.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T07](./tasks/T07-migrate-search-jobs-dispatches.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T08](./tasks/T08-learner-day.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T09](./tasks/T09-answer-evaluation.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T10](./tasks/T10-review-scheduler.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T11](./tasks/T11-session-composition.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T12](./tasks/T12-initdata-auth.md) | Identity & Access Engineer | `gpt-6-astra` | `high` |
| [T13](./tasks/T13-onboarding-api.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T14](./tasks/T14-settings-api.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T15](./tasks/T15-start-session.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T47](./tasks/T47-session-limits-quotas.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T16](./tasks/T16-session-continuation.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T17](./tasks/T17-answer-item.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T48](./tasks/T48-word-status-transitions.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T18](./tasks/T18-new-word-card.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T19](./tasks/T19-today-api.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T20](./tasks/T20-dictionary-import.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T21](./tasks/T21-search-forms.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T22](./tasks/T22-dictionary-search-api.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T23](./tasks/T23-content-report-api.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T24](./tasks/T24-answer-dispute.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T25](./tasks/T25-reference-snapshots.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T26](./tasks/T26-auto-verification.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T27](./tasks/T27-editor-queue.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T28](./tasks/T28-applied-changes-rollback.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T29](./tasks/T29-notification-dispatcher.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T30](./tasks/T30-reminders.md) | Backend Architect | `gpt-6-astra` | `high` |
| [T31](./tasks/T31-summaries-digest.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T32](./tasks/T32-bot-webhook.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T33](./tasks/T33-client-telemetry.md) | Backend Architect | `gpt-6.1-sol` | `high` |
| [T34](./tasks/T34-miniapp-skeleton.md) | Frontend Developer | `gpt-6.1-sol` | `high` |
| [T35](./tasks/T35-ui-onboarding.md) | Frontend Developer | `gpt-6.1-sol` | `high` |
| [T36](./tasks/T36-ui-today.md) | Frontend Developer | `gpt-6.1-sol` | `high` |
| [T37](./tasks/T37-ui-session-shell.md) | Frontend Developer | `gpt-6.1-sol` | `high` |
| [T38](./tasks/T38-ui-recall.md) | Frontend Developer | `gpt-6.1-sol` | `high` |
| [T39](./tasks/T39-ui-dictionary.md) | Frontend Developer | `gpt-6.1-sol` | `high` |
| [T40](./tasks/T40-ui-content-report.md) | Frontend Developer | `gpt-6.1-sol` | `high` |
| [T41](./tasks/T41-ui-settings.md) | Frontend Developer | `gpt-6.1-sol` | `high` |
| [T42](./tasks/T42-ui-editor.md) | Frontend Developer | `gpt-6.1-sol` | `high` |
| [T43](./tasks/T43-retire-group-code.md) | Minimal Change Engineer | `gpt-6.1-sol` | `high` |
| [T44](./tasks/T44-migrate-drop-group-model.md) | Database Reliability Engineer | `gpt-6-astra` | `high` |
| [T45](./tasks/T45-deployment-fixes.md) | DevOps Automator | `gpt-6.1-sol` | `high` |
| [T46](./tasks/T46-e2e-critical-flows.md) | Test Automation Engineer | `gpt-6.1-sol` | `high` |
| [T49](./tasks/T49-load-test.md) | Performance Benchmarker | `gpt-6.1-sol` | `high` |
