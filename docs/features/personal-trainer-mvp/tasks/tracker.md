# Tracker — personal-trainer-mvp

Контент підготовлено: [2 000 схвалених лем](../../../dictionary/README.md), стан — [readiness.json](../../../dictionary/readiness.json). Реалізація імпортера [T20](T20-dictionary-import.md) ще `todo`; підготовка CSV не завершує цю задачу.

Рекомендовані Agency-агенти й моделі: [agent-routing.md](../agent-routing.md). Машинні призначення — `execution` у [tasks.json](./tasks.json).

> Статус кожної задачі епіку. `implement-tasks` оновлює `done`, коли комітить задачу.
> Стани: `todo` · `in_progress` · `blocked` · `review` · `done`. Оцінка: S = 2 год, M = пів дня, L = день.

| # | Task | Layer | Owner | Estimate | Blocked by | Status |
|---|---|---|---|---|---|---|
| [T01](./T01-module-skeleton.md) | Створити каркас feature-модулів, Platform і NodaTime | wiring | Viacheslav Melnichenko | M | — | done |
| [T02](./T02-test-harness.md) | Додати інтеграційні тести на PostgreSQL з FakeClock і фікстурами | tests | Viacheslav Melnichenko | M | [T01](T01-module-skeleton.md) | done |
| [T03](./T03-migrate-identity.md) | Перенести міграції learner і нагадувань | migration | Viacheslav Melnichenko | S | [T01](T01-module-skeleton.md) | done |
| [T04](./T04-migrate-content-core.md) | Перенести міграції записів, версій, перекладів і прийнятих відповідей | migration | Viacheslav Melnichenko | M | [T03](T03-migrate-identity.md) | in_progress |
| [T05](./T05-migrate-learning.md) | Перенести міграції слів learner, session, карток і журналу відповідей | migration | Viacheslav Melnichenko | M | [T04](T04-migrate-content-core.md) | todo |
| [T06](./T06-migrate-reports-publication.md) | Перенести міграції content reports, нейтралізацій, доказів і журналу публікацій | migration | Viacheslav Melnichenko | M | [T05](T05-migrate-learning.md) | todo |
| [T07](./T07-migrate-search-jobs-dispatches.md) | Перенести міграції форм пошуку, довідок, задач звірки й відправлень | migration | Viacheslav Melnichenko | M | [T06](T06-migrate-reports-publication.md) | todo |
| [T08](./T08-learner-day.md) | Реалізувати обчислення доби learner зі зміною поясу | domain | Viacheslav Melnichenko | M | [T01](T01-module-skeleton.md) | done |
| [T09](./T09-answer-evaluation.md) | Реалізувати оцінку відповіді з нормалізацією й різницею написання | domain | Viacheslav Melnichenko | M | [T01](T01-module-skeleton.md) | done |
| [T10](./T10-review-scheduler.md) | Реалізувати планувальник повторень за портом над журналом відповідей | domain | Viacheslav Melnichenko | L | [T01](T01-module-skeleton.md) | blocked |
| [T11](./T11-session-composition.md) | Реалізувати складання session з лімітами й квотами | domain | Viacheslav Melnichenko | M | [T08](T08-learner-day.md), [T10](T10-review-scheduler.md) | todo |
| [T12](./T12-initdata-auth.md) | Автентифікувати запити за initData і додати роль content editor | ports | Viacheslav Melnichenko | M | [T03](T03-migrate-identity.md), [T02](T02-test-harness.md) | todo |
| [T13](./T13-onboarding-api.md) | Реалізувати онбординг і профіль learner | app | Viacheslav Melnichenko | M | [T12](T12-initdata-auth.md) | todo |
| [T14](./T14-settings-api.md) | Реалізувати зміну ліміту, поясу й нагадувань | app | Viacheslav Melnichenko | M | [T13](T13-onboarding-api.md), [T08](T08-learner-day.md) | todo |
| [T15](./T15-start-session.md) | Реалізувати старт session з персистенцією карток | app | Viacheslav Melnichenko | M | [T11](T11-session-composition.md), [T05](T05-migrate-learning.md), [T04](T04-migrate-content-core.md), [T12](T12-initdata-auth.md) | todo |
| [T47](./T47-session-limits-quotas.md) | Застосувати денний ліміт, зміну поясу й квоти під час старту session | app | Viacheslav Melnichenko | M | [T15](T15-start-session.md), [T08](T08-learner-day.md) | todo |
| [T16](./T16-session-continuation.md) | Реалізувати продовження session і закриття на межі доби | app | Viacheslav Melnichenko | M | [T15](T15-start-session.md) | todo |
| [T17](./T17-answer-item.md) | Реалізувати відповідь на картку з ідемпотентністю | app | Viacheslav Melnichenko | L | [T09](T09-answer-evaluation.md), [T10](T10-review-scheduler.md), [T16](T16-session-continuation.md) | todo |
| [T48](./T48-word-status-transitions.md) | Застосувати переходи word status і control repetition у відповіді | app | Viacheslav Melnichenko | M | [T17](T17-answer-item.md) | todo |
| [T18](./T18-new-word-card.md) | Реалізувати картку нового слова, «Далі» і «Вже знаю» | app | Viacheslav Melnichenko | M | [T17](T17-answer-item.md), [T10](T10-review-scheduler.md) | todo |
| [T19](./T19-today-api.md) | Реалізувати екран «Сьогодні» на сервері | app | Viacheslav Melnichenko | S | [T16](T16-session-continuation.md) | todo |
| [T20](./T20-dictionary-import.md) | Імпортувати редакторський CSV у версійований словник | infra | Viacheslav Melnichenko | L | [T04](T04-migrate-content-core.md), [T06](T06-migrate-reports-publication.md) | todo |
| [T21](./T21-search-forms.md) | Генерувати форми пошуку для опублікованих версій | infra | Viacheslav Melnichenko | M | [T20](T20-dictionary-import.md), [T07](T07-migrate-search-jobs-dispatches.md) | todo |
| [T22](./T22-dictionary-search-api.md) | Реалізувати пошук у словнику й картку запису | ports | Viacheslav Melnichenko | M | [T21](T21-search-forms.md), [T12](T12-initdata-auth.md) | todo |
| [T23](./T23-content-report-api.md) | Реалізувати надсилання content report з лімітом і дедуплікацією | app | Viacheslav Melnichenko | M | [T06](T06-migrate-reports-publication.md), [T07](T07-migrate-search-jobs-dispatches.md), [T12](T12-initdata-auth.md) | todo |
| [T24](./T24-answer-dispute.md) | Реалізувати «Моя відповідь теж правильна» | app | Viacheslav Melnichenko | M | [T17](T17-answer-item.md), [T23](T23-content-report-api.md) | todo |
| [T25](./T25-reference-snapshots.md) | Завантажити довідкові знімки для звірки | infra | Viacheslav Melnichenko | L | [T07](T07-migrate-search-jobs-dispatches.md) | todo |
| [T26](./T26-auto-verification.md) | Реалізувати фонову звірку content reports | app | Viacheslav Melnichenko | M | [T23](T23-content-report-api.md), [T25](T25-reference-snapshots.md), [T20](T20-dictionary-import.md), [T21](T21-search-forms.md) | todo |
| [T27](./T27-editor-queue.md) | Реалізувати чергу й рішення content editor | app | Viacheslav Melnichenko | M | [T23](T23-content-report-api.md), [T20](T20-dictionary-import.md), [T21](T21-search-forms.md) | todo |
| [T28](./T28-applied-changes-rollback.md) | Реалізувати список застосованих змін і відкат | app | Viacheslav Melnichenko | M | [T27](T27-editor-queue.md) | todo |
| [T29](./T29-notification-dispatcher.md) | Реалізувати dispatcher таблиці відправлень | infra | Viacheslav Melnichenko | M | [T07](T07-migrate-search-jobs-dispatches.md), [T02](T02-test-harness.md), [T32](T32-bot-webhook.md) | todo |
| [T30](./T30-reminders.md) | Реалізувати планування й умови нагадувань | app | Viacheslav Melnichenko | M | [T29](T29-notification-dispatcher.md), [T08](T08-learner-day.md), [T14](T14-settings-api.md), [T16](T16-session-continuation.md) | todo |
| [T31](./T31-summaries-digest.md) | Реалізувати щоденний підсумок learner і дайджест редактора | app | Viacheslav Melnichenko | M | [T29](T29-notification-dispatcher.md), [T23](T23-content-report-api.md) | todo |
| [T32](./T32-bot-webhook.md) | Переробити webhook бота під Mini App | ports | Viacheslav Melnichenko | M | [T03](T03-migrate-identity.md) | todo |
| [T33](./T33-client-telemetry.md) | Приймати клієнтську телеметрію й публікувати серверні метрики | ports | Viacheslav Melnichenko | S | [T01](T01-module-skeleton.md) | done |
| [T34](./T34-miniapp-skeleton.md) | Створити каркас Mini App, що його роздає бекенд | ui | Viacheslav Melnichenko | M | [T01](T01-module-skeleton.md) | in_progress |
| [T35](./T35-ui-onboarding.md) | Зробити екран онбордингу | ui | Viacheslav Melnichenko | M | [T34](T34-miniapp-skeleton.md), [T13](T13-onboarding-api.md) | todo |
| [T36](./T36-ui-today.md) | Зробити екран «Сьогодні» | ui | Viacheslav Melnichenko | M | [T34](T34-miniapp-skeleton.md), [T19](T19-today-api.md), [T33](T33-client-telemetry.md) | todo |
| [T37](./T37-ui-session-shell.md) | Зробити каркас заняття, картку нового слова й підсумок | ui | Viacheslav Melnichenko | L | [T36](T36-ui-today.md), [T18](T18-new-word-card.md) | todo |
| [T38](./T38-ui-recall.md) | Зробити вправу пригадування, результат і оскарження | ui | Viacheslav Melnichenko | L | [T37](T37-ui-session-shell.md), [T17](T17-answer-item.md), [T24](T24-answer-dispute.md) | todo |
| [T39](./T39-ui-dictionary.md) | Зробити пошук у словнику й картку запису | ui | Viacheslav Melnichenko | M | [T34](T34-miniapp-skeleton.md), [T22](T22-dictionary-search-api.md) | todo |
| [T40](./T40-ui-content-report.md) | Зробити форму content report | ui | Viacheslav Melnichenko | M | [T39](T39-ui-dictionary.md), [T23](T23-content-report-api.md) | todo |
| [T41](./T41-ui-settings.md) | Зробити екран налаштувань | ui | Viacheslav Melnichenko | M | [T34](T34-miniapp-skeleton.md), [T14](T14-settings-api.md) | todo |
| [T42](./T42-ui-editor.md) | Зробити чергу content editor і список застосованих змін | ui | Viacheslav Melnichenko | L | [T34](T34-miniapp-skeleton.md), [T27](T27-editor-queue.md), [T28](T28-applied-changes-rollback.md) | todo |
| [T43](./T43-retire-group-code.md) | Видалити код групової моделі | wiring | Viacheslav Melnichenko | M | [T32](T32-bot-webhook.md), [T07](T07-migrate-search-jobs-dispatches.md), [T20](T20-dictionary-import.md), [T34](T34-miniapp-skeleton.md) | todo |
| [T44](./T44-migrate-drop-group-model.md) | Перенести міграцію видалення таблиць групової моделі | migration | Viacheslav Melnichenko | S | [T43](T43-retire-group-code.md), [T07](T07-migrate-search-jobs-dispatches.md) | todo |
| [T45](./T45-deployment-fixes.md) | Оновити збірку й маніфести розгортання | wiring | Viacheslav Melnichenko | S | [T34](T34-miniapp-skeleton.md) | todo |
| [T46](./T46-e2e-critical-flows.md) | Покрити наскрізними тестами критичні потоки через UI | tests | Viacheslav Melnichenko | M | [T35](T35-ui-onboarding.md), [T38](T38-ui-recall.md), [T40](T40-ui-content-report.md), [T41](T41-ui-settings.md), [T45](T45-deployment-fixes.md) | todo |
| [T49](./T49-load-test.md) | Перевірити бюджети латентності під навантаженням | tests | Viacheslav Melnichenko | M | [T45](T45-deployment-fixes.md), [T17](T17-answer-item.md), [T19](T19-today-api.md), [T22](T22-dictionary-search-api.md), [T30](T30-reminders.md), [T33](T33-client-telemetry.md), [T36](T36-ui-today.md) | todo |

**Total:** 49 tasks, ~26.75 person-days.

## Результати перевірок

- [T01](T01-module-skeleton.md): Release build успішний; 39/39 тестів; навмисна залежність Learning від внутрішнього типу Content спричинила падіння архітектурного тесту, після вилучення — зелений прогін. Нова модель використовує snake_case і NodaTime; runtime/design-time snapshot без змін legacy-схеми. Форматування нових файлів і перевірка markdown-посилань проходять. Залишилися початкові 2 NU1903 та 49 WHITESPACE у старих файлах.

- [T09](T09-answer-evaluation.md): незалежно перевірено 105/105 тестів оцінювання, нормалізації й diff; переглянуто всі доменні файли. Build успішний із початковими NU1903; scoped format і diff check без помилок. Усі результати AC-12–AC-16, AC-56, AC-57 покриті.

- [T02](T02-test-harness.md): harness на PostgreSQL 16 застосовує міграції й очищує стан; контрактні перевірки виявляють невалідні HTTP-відповіді й рядки черг. 14/14 цільових і 180/180 спільних Release-тестів без пропусків; build та scoped format успішні. Відсутній Docker локально дає пояснений skip, у CI — помилку; додано backend PR workflow. Виправлено лише лапки в YAML description, що блокувало парсер. Початкові NU1903 зберігаються.

- [T08](T08-learner-day.md): незалежно перевірено 22/22 часові тести й переглянуто реалізацію. Варшава → Токіо/Нью-Йорк, DST, fallback, точні межі та повторні зміни поясу покриті; сервіс використовує IClock без БД. Спільні Release build і 180/180 тестів зелені. Для інтеграції потрібно передавати попередній знімок доби; межа вже створеної session незмінна.

- [T10](T10-review-scheduler.md): реалізація триває; очікується рішення користувача про суперечність меж інтервалу, коли фактичний попередній інтервал ≥180 діб (нижня prev+1 перевищує стелю 180). До рішення задача не може бути done.

- [T10](T10-review-scheduler.md): переглянуто порт, MEMORIZE і проєкцію журналу; незалежні 48/48 тестів зелені, scoped format успішний. Часткову реалізацію зафіксовано; для prev≥180 повертається явний ReviewIntervalConflictException до рішення користувача. Повний DoD не виконано; залежні задачі не розблоковано.

- [T03](T03-migrate-identity.md): переглянуто сутності, конфігурації, обидві SQL-міграції та generated snapshot; SQL відповідає staged-парам. Незалежно Release build успішний, 10/10 цільових тестів на PostgreSQL 16 без пропусків: чиста БД → up → down0 без залишків → up, схема, NodaTime, унікальність, cascade і xmin. Check-міграція порожня; scoped format та diff check успішні, початкові NU1903 зберігаються.

- [T33](T33-client-telemetry.md): переглянуто controller, валідацію, гістограми й middleware; незалежно 43/43 HTTP/metrics/architecture тестів. Невідомі метрики й невалідний payload дають telemetry.invalid_metric, endpoint потребує автентифікації за контрактом. Development loopback /metrics показує гістограми today/answer/search без персональних даних; Program wiring підключено. Release build і scoped format проходять, залишилися початкові NU1903.
