---
status: Draft
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-11-01"
feature_size: "L"
---

# Test plan — personal-trainer-mvp

Персональний тренер польських слів у Telegram Mini App: онбординг, щоденні session з адаптивними повтореннями, пошук у версійованому словнику, content reports з автоматичною звіркою, черга й відкат для content editor, нагадування й підсумки. План прив'язує кожен AC з [PRD.md §5](./PRD.md#5-acceptance-criteria) до тестів; колонка «Task» вказує задачу з [tasks/tasks.json](./tasks/tasks.json), у якій `implement-tasks` пише червоний тест.

## Levels

`target_surfaces: [backend-service, web-frontend]` ([sad.md](./sad.md)) — тому, крім серверних рівнів, є рівні Mini App.

| Level | Scope | Strategy (generic — no tool names) |
|---|---|---|
| Unit | Чисті доменні правила: доба learner, нормалізація й оцінка відповіді, межі інтервалу, склад session, ключ дедуплікації. | У пам'яті, фіксований годинник, без БД і мережі. |
| Integration | Модуль проти справжнього PostgreSQL: транзакції відповіді, унікальні обмеження, захоплення рядків черги з пропуском заблокованих, межа доби. | Ефемерний PostgreSQL 16 у контейнері на набір тестів; фіксований годинник; підписані тестові `initData`. |
| Contract | Межа Mini App ↔ бекенд і модулі ↔ таблиці-черги. | Кожна відповідь API валідується проти [openapi.yaml](./contracts/openapi.yaml); рядки `verification_jobs` і `notification_dispatches` — проти [events.md](./contracts/events.md); TS-типи клієнта генеруються з того самого контракту. |
| E2E-through-UI | Критичний потік, проведений через справжній інтерфейс Mini App проти справжнього бекенду. | Імітоване середовище Telegram з підписаними `initData`, бекенд на ефемерному PostgreSQL. |
| Component | Екран чи компонент Mini App ізольовано: стан → відмальований результат і взаємодії. | Рендер у тестовому harness з підміною API-клієнта на згенеровані з контракту відповіді. |
| Visual-regression | Ключові екрани у світлій і темній темі Telegram. | Знімок рендера проти затвердженого еталона; еталон оновлюється лише свідомо. |
| Load | NFR латентності й обсягу пілоту з [PRD §6](PRD.md#6-non-functional-requirements). | Інструмент навантаження, який уже є в репозиторії, або відповідний для стеку. |
| E2E (API) | <!-- N/A: критичні потоки покриті E2E-through-UI, а серверні гілки — integration; окремий API-рівень дублював би їх. --> | — |

## AC coverage

Очікуваний результат описано бізнесовими словами — без статусів, кодів помилок і SQL.

| AC ([PRD.md §5](PRD.md#5-acceptance-criteria)) | Test name (intent-based) | Level | Task | Expected outcome |
|---|---|---|---|---|
| AC-01 онбординг | новий learner з лімітом 5 без нагадування бачить «Сьогодні» | integration + e2e-through-UI | [T13](tasks/T13-onboarding-api.md), [T35](tasks/T35-ui-onboarding.md), [T46](tasks/T46-e2e-critical-flows.md) | learner створено з лімітом 5, без reminder, з визначеним поясом |
| AC-01 онбординг | невизначений пояс дає добу за Варшавою | unit + integration | [T08](tasks/T08-learner-day.md), [T13](tasks/T13-onboarding-api.md) | доба рахується за Варшавою, пояс позначено як не вибраний |
| AC-02 нагадування без днів | нагадування без жодного дня не зберігається | integration + component | [T13](tasks/T13-onboarding-api.md), [T35](tasks/T35-ui-onboarding.md) | нагадування не збережено, learner бачить пояснення |
| AC-03 порядок session | повторення йдуть перед новими словами, підсумок показує наступні повторення | unit + integration + e2e-through-UI | [T11](tasks/T11-session-composition.md), [T15](tasks/T15-start-session.md), [T37](tasks/T37-ui-session-shell.md), [T46](tasks/T46-e2e-critical-flows.md) | 4 повторення, потім 3 нові слова, підсумок з датами |
| AC-04 35 повторень | session обрізається до 20 найпростроченіших без нових слів | unit + integration | [T11](tasks/T11-session-composition.md), [T15](tasks/T15-start-session.md) | 20 повторень, 0 нових, решта 15 без mistake |
| AC-05 16 повторень, ліміт 10 | вільні місця заповнюються новими словами, залишок ліміту переходить у наступну session | unit + integration | [T11](tasks/T11-session-composition.md), [T15](tasks/T15-start-session.md) | 16 + 4; ще до 6 нових у наступній session тієї доби |
| AC-06 ліміт вичерпано | друга session доби без нових слів | unit + integration + component | [T11](tasks/T11-session-composition.md), [T47](tasks/T47-session-limits-quotas.md), [T36](tasks/T36-ui-today.md) | лише повторення й пояснення про ліміт |
| AC-07 Варшава → Токіо | після переїзду на схід нова порція з'являється о 08:00 за Токіо | unit + integration | [T08](tasks/T08-learner-day.md), [T47](tasks/T47-session-limits-quotas.md) | до 08:00 Токіо — лише повторення; з 08:00 — нова доба |
| AC-08 пропуск 5 діб | повернення після паузи не додає mistake і додаткових занять | unit + integration | [T10](tasks/T10-review-scheduler.md), [T15](tasks/T15-start-session.md) | session ≤ 20 найпростроченіших, без mistake за пропуск |
| AC-09 квоти | місце отримує квота з найменшою часткою поданих лем, при рівності — everyday | unit + integration | [T11](tasks/T11-session-composition.md), [T47](tasks/T47-session-limits-quotas.md) | послідовність квот збігається з еталонною для фікстури |
| AC-10 курс завершено | без неотриманих approved-лем — лише повторення й позначка «курс завершено» | unit + integration + component | [T11](tasks/T11-session-composition.md), [T47](tasks/T47-session-limits-quotas.md), [T19](tasks/T19-today-api.md), [T36](tasks/T36-ui-today.md) | нових слів немає, «Сьогодні» показує стан курсу |
| AC-10 курс поповнено | нові approved-леми після завершення курсу знову пропонуються | integration + component | [T47](tasks/T47-session-limits-quotas.md), [T36](tasks/T36-ui-today.md) | нові слова з'являються, «Сьогодні» один раз повідомляє про поповнення |
| AC-11 картка нового слова | після карток нових слів іде по одній перевірці без транскрипції | integration + component | [T18](tasks/T18-new-word-card.md), [T37](tasks/T37-ui-session-shell.md) | перевірка після показу, не вважається запланованим повторенням |
| AC-12 правильна відповідь | збіг після нормалізації зараховується | unit + integration + e2e-through-UI | [T09](tasks/T09-answer-evaluation.md), [T17](tasks/T17-answer-item.md), [T38](tasks/T38-ui-recall.md), [T46](tasks/T46-e2e-critical-flows.md) | успіх, показано слово, транскрипцію, приклад, довший інтервал |
| AC-13 порожня відповідь | порожня відповідь не оцінюється | unit + integration + component | [T09](tasks/T09-answer-evaluation.md), [T17](tasks/T17-answer-item.md), [T38](tasks/T38-ui-recall.md) | без оцінки, підказка ввести слово чи «Не знаю» |
| AC-14 «Не знаю» | «Не знаю» — mistake з повторенням наступної доби | unit + integration | [T09](tasks/T09-answer-evaluation.md), [T17](tasks/T17-answer-item.md) | mistake, наступне повторення завтра |
| AC-15 лише діакритика | відмінність у діакритиці — mistake без оскарження | unit + integration + component | [T09](tasks/T09-answer-evaluation.md), [T17](tasks/T17-answer-item.md), [T38](tasks/T38-ui-recall.md) | mistake з позначеною відмінністю, дії оскарження немає |
| AC-16 інша відповідь | відповідь поза варіантами — mistake з оскарженням | unit + integration + component | [T09](tasks/T09-answer-evaluation.md), [T17](tasks/T17-answer-item.md), [T38](tasks/T38-ui-recall.md) | mistake, diff, дія «Моя відповідь теж правильна» доступна |
| AC-17 оскарження | оскарження знімає mistake і створює content report | integration + e2e-through-UI | [T24](tasks/T24-answer-dispute.md), [T38](tasks/T38-ui-recall.md), [T46](tasks/T46-e2e-critical-flows.md) | стан слова як до відповіді, report створено, слово не повертається до кінця доби, з наступної доби йде з попередньою датою повторення; подальше рішення щодо report результат відповіді не змінює |
| AC-17 вікно оскарження | дія доступна після повторного відкриття trainer на тій самій картці, але не після переходу на наступну чи кінця доби | integration + component | [T24](tasks/T24-answer-dispute.md), [T38](tasks/T38-ui-recall.md) | у межах вікна дія виконується, поза ним — відхилена без зміни стану |
| AC-18 «Вже пригадую» | дві правильні відповіді в різних запланованих повтореннях дають «Вже пригадую» | unit + integration | [T10](tasks/T10-review-scheduler.md), [T48](tasks/T48-word-status-transitions.md) | статус змінено, recalled words +1 |
| AC-19 mistake після «Вже пригадую» | mistake повертає слово «На повторення» | unit + integration | [T10](tasks/T10-review-scheduler.md), [T48](tasks/T48-word-status-transitions.md) | статус «На повторення», лічильник поспіль обнулено |
| AC-20 «Вже знаю» < 10 | «Вже знаю» відкладає слово й додає заміну | integration + component | [T18](tasks/T18-new-word-card.md), [T37](tasks/T37-ui-session-shell.md) | control repetition через 7 діб, заміна в session, не рахується до ліміту |
| AC-21 «Вже знаю» ≥ 10 | 11-те «Вже знаю» без заміни рахується до ліміту | unit + integration + component | [T11](tasks/T11-session-composition.md), [T18](tasks/T18-new-word-card.md), [T37](tasks/T37-ui-session-shell.md) | заміни немає, у наступній session доби нових менше на 1 |
| AC-22 control repetition з mistake | mistake на control repetition повертає слово в звичайний графік | unit + integration | [T10](tasks/T10-review-scheduler.md), [T48](tasks/T48-word-status-transitions.md) | mistake, звичайний адаптивний графік |
| AC-23 control repetition правильно | правильна відповідь — перше з двох повторень, інтервал > 7 діб | unit + integration | [T10](tasks/T10-review-scheduler.md), [T48](tasks/T48-word-status-transitions.md) | наступний інтервал довший за 7 діб |
| AC-24 продовження session | перерване заняття продовжується з наступної картки | integration + component | [T16](tasks/T16-session-continuation.md), [T37](tasks/T37-ui-session-shell.md) | без повторів і нових слів понад заплановані |
| AC-25 межа доби | незавершена session закривається на межі доби, зокрема після зміни поясу | unit + integration | [T08](tasks/T08-learner-day.md), [T16](tasks/T16-session-continuation.md) | не completed; показані нові — у ліміті тієї доби, непоказані — у черзі |
| AC-26 немає роботи чи session завершено | нагадування не надсилається, якщо session доби завершено або немає ні повторень, ні нових слів | unit + integration | [T30](tasks/T30-reminders.md) | повідомлення немає |
| AC-26 нічого не опрацьовано | о 19:00 без опрацьованих карток іде нагадування «почати» | unit + integration | [T30](tasks/T30-reminders.md) | одне нагадування з переходом до заняття о 19:00 |
| AC-26 активний learner | остання відповідь о 18:50 відкладає нагадування «продовжити» до 19:20; без відповідей далі воно не пізніше 21:00 | unit + integration | [T30](tasks/T30-reminders.md) | одне нагадування «продовжити» о 19:20, межа 21:00 і кінець доби не перевищуються |
| AC-26 завершено під час відкладення | session завершено до відкладеного часу | unit + integration | [T30](tasks/T30-reminders.md) | відкладене нагадування не надсилається |
| AC-26 нагадування | рядок відправлення відповідає контракту події | contract | [T29](tasks/T29-notification-dispatcher.md), [T30](tasks/T30-reminders.md) | поля й ключ ідемпотентності збігаються з events.md |
| AC-27 нагадування без поясу | нагадування не зберігається без вибраного поясу | integration + component | [T14](tasks/T14-settings-api.md), [T35](tasks/T35-ui-onboarding.md), [T41](tasks/T41-ui-settings.md) | прохання вибрати пояс, нагадування не збережено |
| AC-28 прогрес | «Сьогодні» показує слова до повторення, 12 recalled words і 3 completed sessions за поточну добу й 6 попередніх дат у поясі learner | integration + component + visual-regression | [T19](tasks/T19-today-api.md), [T36](tasks/T36-ui-today.md) | лічильники збігаються з фікстурою; session 7-ї попередньої дати не рахується |
| AC-29 чужий прогрес | запит з ідентичністю іншого learner не бачить чужих даних | integration | [T12](tasks/T12-initdata-auth.md), [T19](tasks/T19-today-api.md) | дані іншого learner приховано як неіснуючі |
| AC-30 пошук | пошук за формою з діакритикою й без неї, 1 символ — точний збіг | unit + integration + component | [T21](tasks/T21-search-forms.md), [T22](tasks/T22-dictionary-search-api.md), [T39](tasks/T39-ui-dictionary.md) | точний збіг першим, далі за base course |
| AC-31 немає збігів | порожній результат пропонує повідомити про відсутній запис | integration + component | [T22](tasks/T22-dictionary-search-api.md), [T39](tasks/T39-ui-dictionary.md), [T40](tasks/T40-ui-content-report.md) | «Не знайшли слово» з дією повідомлення; report зберігає текст пошукового запиту |
| AC-32 лише approved | записи draft і needs_review не з'являються ні в пошуку, ні в нових словах | integration | [T20](tasks/T20-dictionary-import.md), [T22](tasks/T22-dictionary-search-api.md), [T47](tasks/T47-session-limits-quotas.md) | видно лише approved-записи |
| AC-33 зняття approved | знятий запис зникає з пошуку, нових session, лічильників і пропускається в активній session | integration | [T20](tasks/T20-dictionary-import.md), [T22](tasks/T22-dictionary-search-api.md), [T47](tasks/T47-session-limits-quotas.md), [T17](tasks/T17-answer-item.md), [T19](tasks/T19-today-api.md) | без публікації; картка пропущена без зарахування; історія learner лишається; після повторного схвалення слово повертається в розклад з тією самою історією |
| AC-34 content report | report біля картки приймається й підтверджується learner | integration + e2e-through-UI | [T23](tasks/T23-content-report-api.md), [T40](tasks/T40-ui-content-report.md), [T46](tasks/T46-e2e-critical-flows.md) | «Дякуємо, передали на перевірку»; report чекає звірки чи рішення; картка для всіх learner лишається незмінною до підтвердження |
| AC-35 автоматична звірка | підтверджене довідкою значення публікує нову версію | integration | [T25](tasks/T25-reference-snapshots.md), [T26](tasks/T26-auto-verification.md) | нова опублікована версія, доказ звірки, report закрито |
| AC-35 звірка недоступна | три невдалі спроби переводять report у чергу редактора | integration | [T26](tasks/T26-auto-verification.md) | черга редактора з причиною «джерело недоступне» |
| AC-35 задача звірки | рядок задачі відповідає контракту події | contract | [T23](tasks/T23-content-report-api.md), [T26](tasks/T26-auto-verification.md) | поля збігаються з events.md |
| AC-36 без звірки | польська форма, приклад, відсутній запис чи report без значення йдуть одразу в чергу | integration | [T23](tasks/T23-content-report-api.md) | задачі звірки немає, report у черзі |
| AC-36 оскарження без звірки | report з «Моя відповідь теж правильна» йде в чергу без звірки | integration | [T24](tasks/T24-answer-dispute.md) | задачі звірки немає, report у черзі редактора |
| AC-37 підсумок | о 12:00 learner отримує результати, закриті після попереднього підсумку | integration | [T31](tasks/T31-summaries-digest.md) | одне повідомлення; без нових результатів — нічого; результат з ненадісланого — у наступному |
| AC-38 схвалення | схвалення редактором публікує нову версію | integration + component | [T27](tasks/T27-editor-queue.md), [T42](tasks/T42-ui-editor.md) | нова версія, report закрито; схвалений варіант з оскарження не перераховує попередніх відповідей learner |
| AC-39 відхилення | відхилення без причини не приймається, з причиною — закриває report | integration + component | [T27](tasks/T27-editor-queue.md), [T42](tasks/T42-ui-editor.md) | запис не змінено, report закрито з причиною |
| AC-40 додати до кандидатів | відсутній запис додається як draft | integration | [T27](tasks/T27-editor-queue.md) | лема в статусі draft, report закрито |
| AC-41 без ролі | learner без ролі не бачить черги й не змінює словник | integration + component | [T12](tasks/T12-initdata-auth.md), [T27](tasks/T27-editor-queue.md), [T42](tasks/T42-ui-editor.md) | доступ заборонено, словник не змінено |
| AC-42 публікація підтвердженої зміни | звірка чи схвалення публікують нову версію запису | integration | [T26](tasks/T26-auto-verification.md), [T27](tasks/T27-editor-queue.md) | нова опублікована версія, попередні версії лишаються |
| AC-42 новий переклад у розкладі | після публікації нової версії learner бачить новий переклад, історія й статус зберігаються | integration | [T17](tasks/T17-answer-item.md) | новий переклад у картці й оцінці, word status і історія відповідей без змін |
| AC-43 дайджест | о 9:00 за Варшавою редактор отримує дайджест, якщо є reports без рішення | integration | [T31](tasks/T31-summaries-digest.md) | одне повідомлення або жодного |
| AC-44 зміна ліміту й поясу | новий ліміт діє з наступної доби, пояс — одразу | unit + integration + component | [T08](tasks/T08-learner-day.md), [T14](tasks/T14-settings-api.md), [T41](tasks/T41-ui-settings.md) | у поточну добу старий ліміт, з наступної — новий |
| AC-44 нагадування після зміни поясу | нагадування о 19:00 за новим поясом, не більше одного за добу | integration | [T30](tasks/T30-reminders.md) | за добу одне нагадування, навіть якщо вже надіслане за старим поясом |
| AC-45 недопустимий ліміт | ліміт не 3, 5 чи 10 не зберігається | integration + component | [T14](tasks/T14-settings-api.md), [T41](tasks/T41-ui-settings.md) | значення відхилено з переліком допустимих |
| AC-46 ліміт reports | 21-й report за добу не приймається, форма зберігає введене | integration + component | [T23](tasks/T23-content-report-api.md), [T40](tasks/T40-ui-content-report.md) | відмова з поясненням про 00:00, нічого не змінено |
| AC-47 оскарження понад ліміт | оскарження доступне після 20 reports і до ліміту не рахується | integration | [T24](tasks/T24-answer-dispute.md) | report створено, лічильник ліміту не змінився |
| AC-48 відкат | редактор відкочує поточну застосовану зміну | integration + component | [T28](tasks/T28-applied-changes-rollback.md), [T42](tasks/T42-ui-editor.md) | попередня версія опублікована, рядок відкату в журналі, word status і report без змін |
| AC-49 відкат старішої | зміна, після якої з'явилась новіша версія, не відкочується | integration | [T28](tasks/T28-applied-changes-rollback.md) | відмова з поясненням, вказівник не змінено |
| AC-50 відкат без ролі | learner без ролі не бачить змін і не відкочує | integration + component | [T12](tasks/T12-initdata-auth.md), [T28](tasks/T28-applied-changes-rollback.md), [T42](tasks/T42-ui-editor.md) | доступ заборонено, запис не змінено |
| AC-51 повтор report | ідентичний повтор відкритого report не створює нового | unit + integration | [T23](tasks/T23-content-report-api.md) | один report, одна задача звірки, до ліміту зараховано одне повідомлення |
| AC-51 повтор у черзі й підсумку | повтор відкритого report видно один раз | integration | [T27](tasks/T27-editor-queue.md), [T31](tasks/T31-summaries-digest.md) | один рядок у черзі редактора, один результат у підсумку learner |
| AC-52 дія після межі доби | відповідь, «Не знаю», «Далі», «Вже знаю» після межі доби не зараховуються | integration + component | [T16](tasks/T16-session-continuation.md), [T17](tasks/T17-answer-item.md), [T18](tasks/T18-new-word-card.md), [T37](tasks/T37-ui-session-shell.md) | session закрито, стан і ліміти без змін, learner бачить «Сьогодні» |
| AC-53 повторний онбординг | повторне завершення онбордингу повертає наявного learner | integration + component | [T13](tasks/T13-onboarding-api.md), [T35](tasks/T35-ui-onboarding.md) | другого learner немає, налаштування без змін |
| AC-54 Варшава → Нью-Йорк | після переїзду на захід доба подовжується до опівночі Нью-Йорка | unit + integration | [T08](tasks/T08-learner-day.md), [T47](tasks/T47-session-limits-quotas.md) | нова порція лише після 00:00 Нью-Йорка |
| AC-55 оскарження перевірки нового слова | оскарження на перевірці нового слова лишає перше повторення наступної доби | integration | [T24](tasks/T24-answer-dispute.md) | report створено, повторення завтра |
| AC-56 синонім | лема іншого запису з тим самим перекладом — нейтральна відповідь | unit + integration + component | [T09](tasks/T09-answer-evaluation.md), [T17](tasks/T17-answer-item.md), [T38](tasks/T38-ui-recall.md) | ні успіх, ні mistake, report не створено, слово не повертається до кінця доби |
| AC-58 тема | ручний вибір теми зберігається і не змінює навчальний стан | component + visual-regression | [T34](tasks/T34-miniapp-skeleton.md), [T41](tasks/T41-ui-settings.md) | дві теми Everforest, локальне збереження й fallback за design-system.md |
| AC-57 кирилиця | залишок кириличних літер після нормалізації не оцінюється | unit + integration + component | [T09](tasks/T09-answer-evaluation.md), [T17](tasks/T17-answer-item.md), [T38](tasks/T38-ui-recall.md) | без оцінки, прохання змінити розкладку |

**AC-58 — локальні перевірки тем ([T34](tasks/T34-miniapp-skeleton.md), [T41](tasks/T41-ui-settings.md)).** Component: ручне перемикання світлої/темної теми Everforest, збереження після повторного відкриття, пріоритет ручного вибору над themeChanged; відсутній/некоректний запис, недоступний localStorage, fallback Telegram → ОС → світла; перемикання не скидає ввід і стан session та не надсилає серверної мутації. Visual-regression: еталони обох тем, зокрема modal/portal, focus, disabled та error. Unit: контраст пар тексту й контролів за design-system.md; раннє застосування теми перевіряється до першого видимого рендера.

**Контракт усіх операцій.** Механізм перевірки будує [T02](tasks/T02-test-harness.md). Одна contract-сюїта проганяє кожну з 19 операцій [openapi.yaml](./contracts/openapi.yaml) і перевіряє, що успішні й помилкові відповіді збігаються зі схемами й конвертом помилки. Сюїта росте разом із задачами шару `ports` і `app`; клієнт Mini App компілюється з типами, згенерованими з того самого файлу ([T34](tasks/T34-miniapp-skeleton.md)).

**Visual-regression.** Механізм еталонів будує [T34](tasks/T34-miniapp-skeleton.md). Еталони у світлій і темній темі для екранів «Сьогодні», картки нового слова, результату відповіді з diff і черги редактора ([T36](tasks/T36-ui-today.md), [T37](tasks/T37-ui-session-shell.md), [T38](tasks/T38-ui-recall.md), [T42](tasks/T42-ui-editor.md)).

## Edge cases / error paths

Кожен error- і authorization-AC має власний рядок вище: AC-02, AC-13, AC-27, AC-29, AC-31, AC-39, AC-41, AC-45, AC-46, AC-49, AC-50, AC-52, AC-57. Додаткові випадки, які випливають з PRD, SAD і контракту:

- Підроблені чи старші за 24 год `initData` → доступ заборонено, дані не читаються ([T12](tasks/T12-initdata-auth.md), integration).
- Запит без онбордингу до «Сьогодні» чи session → learner просять спершу налаштувати тренер ([T13](tasks/T13-onboarding-api.md), integration).
- Та сама відповідь двічі, зокрема паралельно з двох пристроїв → одна подія відповіді, той самий результат ([T17](tasks/T17-answer-item.md), integration; QG-1).
- Інша відповідь на вже оцінену картку → відмова без зміни результату ([T17](tasks/T17-answer-item.md), integration).
- Паралельний старт двох session → одна активна session ([T15](tasks/T15-start-session.md), integration).
- Перехід на літній і зимовий час у день нагадування → одне нагадування в правильний місцевий час ([T08](tasks/T08-learner-day.md), [T30](tasks/T30-reminders.md), unit + integration).
- Два паралельні тики dispatcher → жоден рядок не надсилається двічі ([T29](tasks/T29-notification-dispatcher.md), integration).
- Простій понад 60 хв → прострочене відправлення позначено як пропущене й не надсилається ([T29](tasks/T29-notification-dispatcher.md), integration).
- Telegram повертає «бот заблоковано» → нагадування learner вимикаються ([T29](tasks/T29-notification-dispatcher.md), [T32](tasks/T32-bot-webhook.md), integration).
- Збій парсера довідкового джерела → report іде в ручну чергу, а не падає звірка ([T25](tasks/T25-reference-snapshots.md), unit).
- Повторне рішення редактора щодо закритого report → відмова, стан без змін ([T27](tasks/T27-editor-queue.md), integration).
- Конкурентний відкат і нова публікація того самого запису → один з них відхилено, вказівник узгоджений ([T28](tasks/T28-applied-changes-rollback.md), integration).
- Застарілий пошуковий запит при швидкому введенні → результат попереднього запиту не перезаписує новий ([T39](tasks/T39-ui-dictionary.md), component).
- Коментар content report з HTML чи скриптом → редактор бачить звичайний текст ([T42](tasks/T42-ui-editor.md), component).
- Webhook без правильного секрету → оновлення відхилено ([T32](tasks/T32-bot-webhook.md), integration).
- Чужі session чи картка в відповіді, «Далі» чи «Вже знаю» → «не знайдено», нічого не змінено ([T17](tasks/T17-answer-item.md), [T18](tasks/T18-new-word-card.md), integration).
- Оскарження чужої відповіді → «не знайдено», нічого не змінено ([T24](tasks/T24-answer-dispute.md), integration).
- Черга редактора показує лише псевдонімну мітку автора, без імені й Telegram id ([T27](tasks/T27-editor-queue.md), integration + contract).
- `initData` і заголовок авторизації не потрапляють у логи ні на успішному, ні на відхиленому запиті ([T12](tasks/T12-initdata-auth.md), integration).
- Повторний імпорт CSV без змін → нових версій немає ([T20](tasks/T20-dictionary-import.md), integration).

## Test data

- **Seed strategy:** фабрики з [data-model.md](./data-model.md#test-fixtures) — `LearnerFixtures.NewLearner(timeZone, dailyLimit)`, `ContentFixtures.NewPublishedEntry(lemmaPl, translationUk, acceptedAnswers)`, `LearningFixtures.NewActiveSession(learner, items)`, `ContentFixtures.NewContentReport(learner, entry, origin)`; доповнюються в задачах, яким потрібні ([T02](tasks/T02-test-harness.md)). Для пошуку й load — згенерований словник на 2 000 записів і learner з 1 000 слів.
- **Час:** фіксований годинник у кожному тесті; сценарії AC-07, AC-25, AC-26, AC-44, AC-54 явно задають пояс і момент.
- **Ідентичність:** тестовий підписувач `initData` з тестовим токеном бота; Telegram id з діапазону `900000000+`, ім'я `Test User` — без реальних персональних даних.
- **Довідкові джерела:** зафіксовані зразки вікітексту в репозиторії; мережа в тестах не використовується.
- **Integration dependency:** ефемерний PostgreSQL 16 у контейнері з застосованими міграціями — без підміни сховища в пам'яті для integration-рівня.
- **Cleanup boundary:** контейнер на набір тестів, стан очищується перед кожним тестом (усі таблиці фічі, крім довідкових знімків); e2e-through-UI — окрема БД на прогін.

## NFR validation (load)

Інструмент — той, що вже є в репозиторії, або відповідний для стеку ([T49](tasks/T49-load-test.md)). Профіль: один екземпляр, словник на 2 000 записів, 100 learner по 1 000 слів.

- **Обсяг пілоту ≥ 100 одночасних learner** → 100 віртуальних learner 15 хв: відкриття «Сьогодні», session із 20 відповідей, 10 пошуків; помилок немає, а всі пороги нижче виконуються.
- **Latency p95 оцінки відповіді ≤ 500 ms** → у тому самому прогоні серверна метрика оцінки p95 ≤ 500 ms.
- **Latency p95 пошуку ≤ 300 ms** → запит на кожен символ з 1–5 символів, серверна метрика пошуку p95 ≤ 300 ms.
- **Latency p95 «Сьогодні» ≤ 1 500 ms** → під тим самим навантаженням клієнт у браузері відкриває trainer 50 разів; p95 телеметрії `today_ready_ms` ≤ 1 500 ms.
- **Точність нагадувань ≥ 99% у межах 2 хв** → 1 000 нагадувань, запланованих на одну хвилину, з реальним годинником і заглушкою Telegram: ≥ 99% надіслано протягом 2 хв від запланованого часу. Тижневе вікно з [PRD §6](PRD.md#6-non-functional-requirements) далі вимірює журнал доставки в пілоті.
- **0 втрачених відповідей** → integration-тест подвійного й паралельного надсилання ([T17](tasks/T17-answer-item.md)) плюс звірка після load-прогону: кількість подій відповіді дорівнює кількості оцінених карток ([T49](tasks/T49-load-test.md)).
- **Availability 99.5%** — <!-- N/A: місячне SLO вимірює моніторинг пілоту, а не тест. -->

## CI placement

- **На кожен PR у backend CI:** unit, integration на ефемерному PostgreSQL (пропуск через відсутність контейнера в CI вважається падінням), серверні contract-перевірки.
- **Локально за DoD задач Mini App:** build, lint, typecheck, component, e2e-through-UI і visual-regression; до CI їх не додаємо за рішенням від 2026-11-01 ([T34](tasks/T34-miniapp-skeleton.md), [T46](tasks/T46-e2e-critical-flows.md)).
- **Вручну перед пілотом:** один smoke test у справжньому Telegram-клієнті з тестовим ботом і HTTPS-тунелем, без окремого чекліста ([T46](tasks/T46-e2e-critical-flows.md)).
- **Вручну перед пілотом:** load ([T49](tasks/T49-load-test.md)) зі звітом як артефакт CI.
