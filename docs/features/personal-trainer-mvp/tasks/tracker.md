# Tracker — personal-trainer-mvp

> Статус кожної задачі епіку. `implement-tasks` оновлює `done`, коли комітить задачу.
> Стани: `todo` · `in_progress` · `blocked` · `review` · `done`. Оцінка: S = 2 год, M = пів дня, L = день.

| # | Task | Layer | Owner | Estimate | Blocked by | Status |
|---|---|---|---|---|---|---|
| [T01](./T01-module-skeleton.md) | Створити каркас feature-модулів, Platform і NodaTime | wiring | Viacheslav Melnichenko | M | — | todo |
| [T02](./T02-test-harness.md) | Додати інтеграційні тести на PostgreSQL з FakeClock і фікстурами | tests | Viacheslav Melnichenko | M | T01 | todo |
| [T03](./T03-migrate-identity.md) | Перенести міграції learner і нагадувань | migration | Viacheslav Melnichenko | S | T01 | todo |
| [T04](./T04-migrate-content-core.md) | Перенести міграції записів, версій, перекладів і прийнятих відповідей | migration | Viacheslav Melnichenko | M | T03 | todo |
| [T05](./T05-migrate-learning.md) | Перенести міграції слів learner, session, карток і журналу відповідей | migration | Viacheslav Melnichenko | M | T04 | todo |
| [T06](./T06-migrate-reports-publication.md) | Перенести міграції content reports, нейтралізацій, доказів і журналу публікацій | migration | Viacheslav Melnichenko | M | T05 | todo |
| [T07](./T07-migrate-search-jobs-dispatches.md) | Перенести міграції форм пошуку, довідок, задач звірки й відправлень | migration | Viacheslav Melnichenko | M | T06 | todo |
| [T08](./T08-learner-day.md) | Реалізувати обчислення доби learner зі зміною поясу | domain | Viacheslav Melnichenko | M | T01 | todo |
| [T09](./T09-answer-evaluation.md) | Реалізувати оцінку відповіді з нормалізацією й різницею написання | domain | Viacheslav Melnichenko | M | T01 | todo |
| [T10](./T10-review-scheduler.md) | Реалізувати планувальник повторень за портом над журналом відповідей | domain | Viacheslav Melnichenko | L | T01 | todo |
| [T11](./T11-session-composition.md) | Реалізувати складання session з лімітами й квотами | domain | Viacheslav Melnichenko | M | T08, T10 | todo |
| [T12](./T12-initdata-auth.md) | Автентифікувати запити за initData і додати роль content editor | ports | Viacheslav Melnichenko | M | T03, T02 | todo |
| [T13](./T13-onboarding-api.md) | Реалізувати онбординг і профіль learner | app | Viacheslav Melnichenko | M | T12 | todo |
| [T14](./T14-settings-api.md) | Реалізувати зміну ліміту, поясу й нагадувань | app | Viacheslav Melnichenko | M | T13, T08 | todo |
| [T15](./T15-start-session.md) | Реалізувати старт session з персистенцією карток | app | Viacheslav Melnichenko | M | T11, T05, T04, T12 | todo |
| [T47](./T47-session-limits-quotas.md) | Застосувати денний ліміт, зміну поясу й квоти під час старту session | app | Viacheslav Melnichenko | M | T15, T08 | todo |
| [T16](./T16-session-continuation.md) | Реалізувати продовження session і закриття на межі доби | app | Viacheslav Melnichenko | M | T15 | todo |
| [T17](./T17-answer-item.md) | Реалізувати відповідь на картку з ідемпотентністю | app | Viacheslav Melnichenko | L | T09, T10, T16 | todo |
| [T48](./T48-word-status-transitions.md) | Застосувати переходи word status і control repetition у відповіді | app | Viacheslav Melnichenko | M | T17 | todo |
| [T18](./T18-new-word-card.md) | Реалізувати картку нового слова, «Далі» і «Вже знаю» | app | Viacheslav Melnichenko | M | T17, T10 | todo |
| [T19](./T19-today-api.md) | Реалізувати екран «Сьогодні» на сервері | app | Viacheslav Melnichenko | S | T16 | todo |
| [T20](./T20-dictionary-import.md) | Імпортувати редакторський CSV у версійований словник | infra | Viacheslav Melnichenko | L | T04, T06 | todo |
| [T21](./T21-search-forms.md) | Генерувати форми пошуку для опублікованих версій | infra | Viacheslav Melnichenko | M | T20, T07 | todo |
| [T22](./T22-dictionary-search-api.md) | Реалізувати пошук у словнику й картку запису | ports | Viacheslav Melnichenko | M | T21, T12 | todo |
| [T23](./T23-content-report-api.md) | Реалізувати надсилання content report з лімітом і дедуплікацією | app | Viacheslav Melnichenko | M | T06, T07, T12 | todo |
| [T24](./T24-answer-dispute.md) | Реалізувати «Моя відповідь теж правильна» | app | Viacheslav Melnichenko | M | T17, T23 | todo |
| [T25](./T25-reference-snapshots.md) | Завантажити довідкові знімки для звірки | infra | Viacheslav Melnichenko | L | T07 | todo |
| [T26](./T26-auto-verification.md) | Реалізувати фонову звірку content reports | app | Viacheslav Melnichenko | M | T23, T25, T20, T21 | todo |
| [T27](./T27-editor-queue.md) | Реалізувати чергу й рішення content editor | app | Viacheslav Melnichenko | M | T23, T20, T21 | todo |
| [T28](./T28-applied-changes-rollback.md) | Реалізувати список застосованих змін і відкат | app | Viacheslav Melnichenko | M | T27 | todo |
| [T29](./T29-notification-dispatcher.md) | Реалізувати dispatcher таблиці відправлень | infra | Viacheslav Melnichenko | M | T07, T02, T32 | todo |
| [T30](./T30-reminders.md) | Реалізувати планування й умови нагадувань | app | Viacheslav Melnichenko | M | T29, T08, T14, T16 | todo |
| [T31](./T31-summaries-digest.md) | Реалізувати щоденний підсумок learner і дайджест редактора | app | Viacheslav Melnichenko | M | T29, T23 | todo |
| [T32](./T32-bot-webhook.md) | Переробити webhook бота під Mini App | ports | Viacheslav Melnichenko | M | T03 | todo |
| [T33](./T33-client-telemetry.md) | Приймати клієнтську телеметрію й публікувати серверні метрики | ports | Viacheslav Melnichenko | S | T01 | todo |
| [T34](./T34-miniapp-skeleton.md) | Створити каркас Mini App, що його роздає бекенд | ui | Viacheslav Melnichenko | M | T01 | todo |
| [T35](./T35-ui-onboarding.md) | Зробити екран онбордингу | ui | Viacheslav Melnichenko | M | T34, T13 | todo |
| [T36](./T36-ui-today.md) | Зробити екран «Сьогодні» | ui | Viacheslav Melnichenko | M | T34, T19, T33 | todo |
| [T37](./T37-ui-session-shell.md) | Зробити каркас заняття, картку нового слова й підсумок | ui | Viacheslav Melnichenko | L | T36, T18 | todo |
| [T38](./T38-ui-recall.md) | Зробити вправу пригадування, результат і оскарження | ui | Viacheslav Melnichenko | L | T37, T17, T24 | todo |
| [T39](./T39-ui-dictionary.md) | Зробити пошук у словнику й картку запису | ui | Viacheslav Melnichenko | M | T34, T22 | todo |
| [T40](./T40-ui-content-report.md) | Зробити форму content report | ui | Viacheslav Melnichenko | M | T39, T23 | todo |
| [T41](./T41-ui-settings.md) | Зробити екран налаштувань | ui | Viacheslav Melnichenko | M | T34, T14 | todo |
| [T42](./T42-ui-editor.md) | Зробити чергу content editor і список застосованих змін | ui | Viacheslav Melnichenko | L | T34, T27, T28 | todo |
| [T43](./T43-retire-group-code.md) | Видалити код групової моделі | wiring | Viacheslav Melnichenko | M | T32, T07, T20, T34 | todo |
| [T44](./T44-migrate-drop-group-model.md) | Перенести міграцію видалення таблиць групової моделі | migration | Viacheslav Melnichenko | S | T43, T07 | todo |
| [T45](./T45-deployment-fixes.md) | Оновити збірку й маніфести розгортання | wiring | Viacheslav Melnichenko | S | T34 | todo |
| [T46](./T46-e2e-critical-flows.md) | Покрити наскрізними тестами критичні потоки через UI | tests | Viacheslav Melnichenko | M | T35, T38, T40, T41, T45 | todo |
| [T49](./T49-load-test.md) | Перевірити бюджети латентності під навантаженням | tests | Viacheslav Melnichenko | M | T45, T17, T19, T22, T33 | todo |

**Total:** 49 tasks, ~26.75 person-days.
