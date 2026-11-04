---
status: Draft
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead", "Backend Lead"]
updated_at: "2026-11-04"
feature_size: M
stage: "15"
ticket: "TBD"
---

# Test plan — multi-target-languages

Brownfield-дельта: тренер дістає вимір `target_language`. План перевіряє три речі, яких
reference-фіча ([personal-trainer-mvp](../personal-trainer-mvp/test-plan.md)) не покриває —
**вибір і перемикання мови**, **окремий облік прогресу на мову** та **ізоляцію** (крос-learner
+ крос-мовну, ключ `(learner_id, target_language)`, [SAD §8](sad.md#8-crosscutting-concepts)).
Кожен AC з [PRD §5](PRD.md#5-acceptance-criteria) прив'язано до ≥1 тесту; контракт — проти
[openapi.yaml](contracts/openapi.yaml); гілки помилок узято з alt/else критичних flow
[SAD §6](sad.md#6-runtime-view).

> **Колонка «Task».** Задачі ще не нарізано — це робить наступний стейдж `sdlc:break-tasks`.
> До того колонка містить `TBD`; break-tasks замінить її посиланнями на `tasks/tasks.json`
> (той самий патерн, що в reference test-plan), де `implement-tasks` пише червоний тест.

## Levels

`target_surfaces: [backend-service, web-frontend]` — успадковано від reference, тому поряд із
серверними рівнями лишаються рівні Mini App.

| Level | Scope | Strategy (generic — без назв інструментів) |
|---|---|---|
| Unit | Чисті доменні правила без мови: вибір наступних лем із base course **однієї мови** у її порядку, незалежність обліку доби/ліміту/статусів між мовами, доступність мови (`is_enabled` ∧ є approved-контент). | У пам'яті, фіксований годинник, без БД і мережі. |
| Integration | Модуль проти справжнього PostgreSQL: старт/перемикання мови під lock рядка `(learner, lang)`, окремий `learner_language_states` на мову, денормалізований `target_language` на прогресі/черзі, оптимістична конкурентність (`xmin`), **тести ізоляції**. | Ефемерний PostgreSQL 16 у контейнері на набір тестів; застосовані міграції фічі; фіксований годинник; підписані тестові `initData`. |
| Contract | Межа Mini App ↔ бекенд для 5 нових операцій фічі. | Кожна відповідь (2xx + кожен код помилки) валідується проти [openapi.yaml](contracts/openapi.yaml); TS-типи клієнта генеруються з того самого файлу. |
| E2E-through-UI | Критичні потоки через справжній інтерфейс Mini App: вибір мови → «Сьогодні» нової мови; перемикання активної мови. | Імітоване середовище Telegram із підписаними `initData`, бекенд на ефемерному PostgreSQL. |
| Component | Екрани Mini App ізольовано: вибір/перемикання мови, порожній стан нової мови, відсутність чужомовних лічильників. | Рендер у harness із підміною API-клієнта на згенеровані з контракту відповіді. |
| Load | NFR латентності й ізоляції з [PRD §6](PRD.md#6-non-functional-requirements). | Інструмент навантаження з репозиторію або відповідний стеку. |
| Visual-regression | <!-- N/A: фіча не вводить нових екранів-еталонів; перемикач мови й порожній стан покриваються component-рівнем. Еталони reference лишаються дійсними. --> |— |

## AC coverage

Очікуваний результат — бізнесовими словами, без статусів, кодів і SQL. Кожен AC має ≥1 тест.

| AC ([PRD §5](PRD.md#5-acceptance-criteria)) | Test name (intent-based) | Level | Task | Expected outcome |
|---|---|---|---|---|
| AC-01 обрати мову (happy) | старт англійської починає її окремо й показує «Сьогодні» EN із першим заняттям | integration + contract + e2e-through-UI | TBD | створено `learner_language_states(learner, en)`, `active_target_language=en`, «Сьогодні» EN із першими новими словами в межах ліміту |
| AC-02 мова без контенту (error) | вибір мови без жодного approved-слова не починає навчання й лишає активну мову | integration + contract | TBD | мову не почато, пояснення «недоступна», `active_target_language` без змін |
| AC-03 перемкнути активну (happy) | перемикання на EN показує стан EN, облік PL лишається цілим після зворотного перемикання | integration + contract + e2e-through-UI | TBD | «Сьогодні»/заняття/словник/налаштування показують EN; стан PL незмінний і доступний назад |
| AC-04 незавершена session при перемиканні (cross-context) | перемикання мови не чіпає незавершену session першої мови; картка не переходить між мовами | integration | TBD | session PL лишається активною в межах своєї доби; відповіді кожної мови — лише у свій облік; 0 перенесених карток |
| AC-05 окремий ліміт на мову (domain invariant) | вичерпаний ліміт однієї мови не впливає на нові слова другої | unit + integration | TBD | друга мова видає нові слова за власним лімітом; статуси/графік/облік «Вже знаю» незалежні |
| AC-06 base course на мову (domain invariant) | нові слова беруться лише з base course активної мови в її порядку | unit + integration | TBD | леми лише цієї мови за її `course_position`; завершення курсу однієї мови не впливає на іншу |
| AC-07 порожній стан нової мови (happy) | щойно почата мова показує зрозумілий початок без чужомовних лічильників | integration + component + e2e-through-UI | TBD | початковий стан + перші нові слова в межах ліміту; лічильників інших мов не видно |
| AC-08 вимова/форми/нормалізація активної мови (domain invariant) | картка й оцінка використовують підказки, форми й правила нормалізації активної мови | unit + integration | TBD | підказка/приклад/прийняті варіанти/пошук за формами — активної мови; нормалізація за її правилами (фікстура з підтвердженим профілем, OQ-5) |
| AC-09 report до активної мови (cross-context) | content report прив'язується до запису активної мови й лягає в чергу цієї мови | integration + contract | TBD | report із `target_language` активної мови в її черзі; контент інших мов без змін |
| AC-10 чужий прогрес (authorization) | другий learner тієї ж мови не бачить чужих word status і результатів | integration | TBD | видно лише власний облік; дані іншого learner (у будь-якій мові) приховані як неіснуючі |
| AC-11 черга редактора за мовою (cross-context) | черга відокремлює повідомлення за мовою; рішення застосовується лише до словника тієї мови | integration + contract | TBD | фільтр/розмежування за мовою; схвалення/відкат змінює лише словник мови повідомлення |
| AC-12 повторний вибір мови (domain invariant) | повторний старт уже активної мови повертає наявний стан без подвійного обліку | integration + contract | TBD | мову не почато вдруге; word status, історія й облік нових слів без змін |

## Edge cases / error paths

Кожен error-/authz-AC має рядок вище (AC-02, AC-10). Нижче — виділені тести гілок помилок із
контракту й alt-блоків [SAD §6](sad.md#6-runtime-view); **кожна відповідь валідується проти
конверта `{code, message, details?}`**.

- **`language.unknown` → 404** — старт мови з кодом, якого немає в каталозі (`startLearnerLanguage`); нічого не створено (integration + contract).
- **`language.not_available` → 409** — старт вимкненої мови або мови без approved-контенту (AC-02, flow 3); активна мова без змін (integration + contract).
- **`language.not_studying` → 409** — `setActiveLanguage` на мову, яку learner ще не почав (AC-03); активна мова без змін (integration + contract).
- **`language.concurrent_update` → 409** — дві паралельні вкладки стартують/перемикають ту саму мову; конфлікт `xmin` відхиляє один запис, **без подвійного обліку** (AC-12/OQ-4, flow 4) (integration + contract).
- **Ідемпотентність старту → 200** — повторний `startLearnerLanguage` уже веденої мови повертає наявний рядок без змін (AC-12, flow 4, alt «EN уже ведеться») (integration + contract).
- **`learner.not_onboarded` → 404** — `listLearnerLanguages` / `setActiveLanguage` до завершення онбордингу (integration + contract).
- **`auth.invalid_init_data` → 401** — відсутні/підроблені/старші за 24 год `initData` на будь-якій операції; дані не читаються, `initData` не потрапляє в логи (integration + contract).
- **`editor.role_required` → 403** — `listEditorQueueByLanguage` від learner без ролі content editor (integration + contract).
- **`editor.invalid_request` → 400** — фільтр черги з невідомим кодом мови / некоректним курсором (integration + contract).
- **Ліміт повідомлень на мову** — добовий ліміт reports learner (reference AC-46) рахує повідомлення активної мови; розмежування за мовою **не збільшує** сукупного ліміту (flow 6, else-гілка) (integration).

### Isolation (ключ §8 — ризик №1, High)

Виділений блок — бо витік фільтра ізоляції визнано головним ризиком фічі.

- Відповідь у мові EN не з'являється й не рахується в обліку PL, і навпаки (AC-04, AC-05) (integration).
- Лічильники «Сьогодні» активної мови не включають слів/сесій інших мов (AC-07) (integration + component).
- `content_reports`/черга редактора мови A ніколи не містять елементів мови B (AC-11) (integration).
- Запит прогресу/словника з ідентичністю іншого learner не бачить чужих даних у жодній мові (AC-10) (integration).
- **Інваріант денормалізації:** `target_language` рядка `learner_words`/`answer_events`/`learning_sessions`/`content_reports` завжди дорівнює `target_language` його `entry` (де є `entry_id`) — перевіряється app-інваріантом + тестом (не DB-CHECK) ([data-model.md](data-model.md#altered-entities-alter)) (integration).

## Contract tests

Механізм перевірки — спільний із reference ([T02 harness](../personal-trainer-mvp/tasks/T02-test-harness.md), перевикористовується). Одна contract-сюїта проганяє **5 нових операцій** фічі й перевіряє, що успішні й помилкові відповіді збігаються зі схемами [openapi.yaml](contracts/openapi.yaml):

| Operation | Перевіряється |
|---|---|
| `listTargetLanguages` | каталог без пагінації; `available` обчислено сервером; 200/401 |
| `listLearnerLanguages` | `active_target_language` (nullable) + items; порожній список для нової мови; 200/401/404 |
| `startLearnerLanguage` | 201 (почато) / 200 (ідемпотентно) / 404 `language.unknown` / 409 `language.not_available`\|`language.concurrent_update` |
| `setActiveLanguage` | 200 / 401 / 404 `learner.not_onboarded` / 409 `language.not_studying`\|`language.concurrent_update` |
| `listEditorQueueByLanguage` | фільтр `target_language`; кожен елемент несе мову; курсорна пагінація; 200/400/401/403 |

Клієнт Mini App компілюється з типами, згенерованими з того самого контракту. **Крос-файлова дельта** (additive `active_target_language` у `Learner`, `target_language` у `TodaySummary`, серверне виведення мови в `createContentReport`) перевіряється contract-тестами reference-контракту **на інтеграції злиття** — поза цією сюїтою (див. [api-sync-report.md §B point 5](contracts/api-sync-report.md)).

## Test data

- **Seed strategy:** фабрики з [data-model.md §Test fixtures](data-model.md#test-fixtures) —
  `TargetLanguageFixtures.NewEnabledLanguage(code, normalizationProfile)`,
  `LearnerFixtures.NewLearnerStudying(code, dailyLimit)` (learner із рядком `learner_language_states` + `active_target_language`),
  `ContentFixtures.NewPublishedEntry(targetLanguage, lemma, …)`,
  `ContentFixtures.NewContentReport(learner, entry, targetLanguage, origin)`.
  Для ізоляції — learner, що веде **дві** мови одночасно (PL+EN) з непорожнім обліком у кожній.
  Для AC-02 — мова `is_enabled=true` **без** approved-записів і мова `is_enabled=false`.
- **Час:** фіксований годинник; per-language стан доби (`learner_language_states.current_day_*`) задається явно для AC-04/AC-05.
- **Ідентичність:** тестовий підписувач `initData`; Telegram id з діапазону `900000000+`, ім'я `Test User` — без реальних персональних даних (PII guard).
- **Integration dependency:** ефемерний PostgreSQL 16 у контейнері з застосованими міграціями фічі — **без підміни сховища в пам'яті** (mock DB — анти-патерн: конкурентність `xmin`, унікальність `(target_language, external_id)` і lock рядка `(learner, lang)` на моку не відтворюються).
- **Cleanup boundary:** контейнер на набір тестів; стан очищується перед кожним тестом (таблиці фічі + `target_languages` сид повторно застосовується); e2e-through-UI — окрема БД на прогін.

## NFR validation (load)

Інструмент — той, що в репозиторії, або відповідний стеку. Профіль — пілотний: один екземпляр,
learner, що ведуть кілька мов, із непорожнім обліком у кожній.

- **Latency p95 перемикання активної мови ≤ 1 500 ms** ([PRD §6](PRD.md#6-non-functional-requirements)) → сценарій: `setActiveLanguage` + рендер «Сьогодні» нової мови; клієнтська телеметрія від вибору мови до рендера, p95 ≤ 1 500 ms.
- **Latency p95 відкриття «Сьогодні» активної мови ≤ 1 500 ms** → під тим самим навантаженням клієнт відкриває «Сьогодні» 50 разів; p95 телеметрії ≤ 1 500 ms.
- **Latency p95 оцінки відповіді ≤ 500 ms** → session з 20 відповідей активної мови; серверна метрика оцінки p95 ≤ 500 ms (з мовним дискримінатором у запиті).
- **Latency p95 пошуку в словнику активної мови ≤ 300 ms** → запит на кожен символ 1–5 символів із фільтром `target_language`; серверна метрика p95 ≤ 300 ms.
- **Ізоляція прогресу = 0 відповідей не до своєї мови** → integration-тести ізоляції (вище) **плюс** звірка після load-прогону: кількість `answer_events` кожної мови дорівнює кількості оцінених карток цієї мови; жодного запису з «чужим» `target_language`.
- **Availability 99.5%** — <!-- N/A: місячне SLO вимірює моніторинг пілоту, а не тест. -->

## CI placement

- **На кожен PR у backend CI:** unit, integration на ефемерному PostgreSQL (пропуск через відсутність контейнера = падіння), contract-перевірки 5 операцій. `spectral lint contracts/openapi.yaml` додається до цільового `make check` (гейт reference-контракту) — запустити в середовищі з Node (у поточному середовищі відкладено, [api-sync-report.md §Lint](contracts/api-sync-report.md)).
- **Локально за DoD задач Mini App:** build, lint, typecheck, component, e2e-through-UI (рішення reference від 2026-11-01 — до CI не додаємо).
- **Вручну перед пілотом:** load зі звітом як артефакт; один smoke у справжньому Telegram-клієнті з перемиканням мови.

## Open items that touch tests — закрито (2026-11-04)

- **OQ-4 RESOLVED** — конкурентність: оптимістична `xmin`, конфлікт → `409 language.concurrent_update`, **клієнт повторює без server-side retry**. Тест flow 4 закріплює «без подвійного обліку» + гілку 409 на двох паралельних стартах/перемиканнях тієї самої мови.
- **OQ-5 RESOLVED** — каркас нормалізації de/nl кодується одразу (повні правила: рід, артиклі, капіталізація, складені слова). AC-08 тестується на всіх чотирьох мовах із заведеним `normalization_profile`; фікстури de/nl додаються в T07. (OQ-7: джерела відбирає spike-крок T07 — до того en/de/nl лишаються `is_enabled=false`.)
- **OQ-9 RESOLVED** — роль редактора єдина для всіх мов → `listEditorQueueByLanguage` **без per-language authz й без тесту 403-гілки**; покривається лише коректність маршрутизації черги/схвалення за мовою (AC-11).
