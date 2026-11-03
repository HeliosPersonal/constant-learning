---
status: Draft
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-11-03"
feature_size: "M"
---

# Test plan — learned-words-stats

Екран «Мої слова»: перегляд вивчених слів з особистою статистикою (показ / правильно / неправильно / серія / забуте), вкладки за статусом, пошук, сортування й картка слова з розбивкою помилок. Усі лічильники сервер рахує наживо з журналу відповідей за винятком успішно оскаржених помилок, статус слова — з `learner_words` (єдине джерело істини, як на «Сьогодні»). План прив'язує кожен AC з [PRD.md §5](./PRD.md#5-acceptance-criteria) до тестів; колонка «Task» вказує задачу з [tasks/tasks.json](./tasks/tasks.json), у якій `implement-tasks` пише червоний тест.

## Levels

`target_surfaces: [backend-service, web-frontend]` ([personal-trainer-mvp sad.md](../personal-trainer-mvp/sad.md)) — тому, крім серверних рівнів, є рівні Mini App. Фіча не додає таблиць і міграцій: усі лічильники читаються наживо з таблиць, які очищає reset-progress.

| Level | Scope | Strategy (generic — no tool names) |
|---|---|---|
| Unit | Чисті правила: розбір і валідація `tab` / `sort` / `q` / курсору, нормалізація пошуку без діакритики, правило `is_weak`. | У пам'яті, без БД і мережі. |
| Integration | Модуль проти справжнього PostgreSQL: лічильники наживо з журналу з анти-join оскаржень, вкладки зі статусу, пагінація курсором, показ з опублікованої версії, очищення після скидання. | Ефемерний PostgreSQL 16 у контейнері на набір тестів (Testcontainers); фіксований годинник; підписані тестові `initData`. |
| Contract | Межа Mini App ↔ бекенд. | Кожна з трьох операцій валідується проти [openapi.yaml](./contracts/openapi.yaml) і конверта помилки; TS-типи клієнта генеруються з того самого контракту. |
| Component | Екран «Мої слова» ізольовано: стан → відмальований результат і взаємодії. | Рендер у тестовому harness (vitest) з підміною API-клієнта на згенеровані з контракту відповіді. |
| E2E-through-UI | <!-- N/A: критичний потік тренування слабких слів покрито integration (старт extra-practice) + component (дія екрана); окремий e2e дублював би unlimited-learning. --> | — |

## AC coverage

Очікуваний результат описано бізнесовими словами — без статусів, кодів помилок і SQL.

| AC ([PRD.md §5](PRD.md#5-acceptance-criteria)) | Test name (intent-based) | Level | Task | Expected outcome |
|---|---|---|---|---|
| AC-01 «Усі» лише бачені | вкладка «Усі» повертає лише слова зі станом `review` або `recalled` і не розкриває порядок непоказаних слів курсу | integration + component | [T03](tasks/T03-stats-controller-and-request.md), [T04](tasks/T04-ui-my-words-screen.md) | у списку лише побачені слова; жодного слова без відповіді учня з курсу |
| AC-02 визначення лічильників | `shown` рахує лише зараховані спроби (повторення / контрольне / додаткове), `correct` — успішні, серія й забуте — зі стану слова | integration | [T02](tasks/T02-stats-service-counting.md), [T05](tasks/T05-stats-integration-tests.md) | лічильники збігаються з оцінкою тренера; нейтральні й неоцінювані картки не входять у показ |
| AC-03 оскарження не помилка | скасоване через «Моя відповідь теж правильна» оскарження й синонім не рахуються помилкою | integration | [T02](tasks/T02-stats-service-counting.md), [T05](tasks/T05-stats-integration-tests.md) | оскаржений одрук поза `incorrect` і розбивкою; `typo` / `diacritic` / `dont_know` збігаються з журналом |
| AC-04 вкладки й зведення зі «Сьогодні» | зведення й вкладки рахуються з `learner_words.status` — того самого джерела, що «Сьогодні» | integration + component | [T01](tasks/T01-stats-contract-and-errors.md), [T04](tasks/T04-ui-my-words-screen.md) | `seen` = 42, `recalled` = 18, `review` = 24; цифри не розходяться зі «Сьогодні» |
| AC-05 тренування слабких | дія тренування слабких слів запускає наявну дію додаткового тренування, окремого ендпоінта немає | integration + component | [T04](tasks/T04-ui-my-words-screen.md), [T05](tasks/T05-stats-integration-tests.md) | старт через `POST /api/v1/sessions/extra-practice`; правила unlimited-learning не змінено |
| AC-06 картка приховує непоказане | картка слова, якого учень ще не бачив (або чужого/неіснуючого), не існує для нього | integration | [T03](tasks/T03-stats-controller-and-request.md), [T05](tasks/T05-stats-integration-tests.md) | `404 stats.word_not_found`, однаково для непоказаного й чужого — існування не розкрито |
| AC-07 скидання обнуляє статистику | після скидання прогресу список порожній, а зведення — нулі | integration | [T05](tasks/T05-stats-integration-tests.md) | `seen` = `recalled` = `review` = `weak` = 0; окремого кешу поза журналом немає |
| AC-08 слабке слово | `is_weak` = true лише для `review` з забуванням чи помилками; `recalled` ніколи не слабке | integration | [T02](tasks/T02-stats-service-counting.md), [T05](tasks/T05-stats-integration-tests.md) | `weak_count` дорівнює кількості слів з `is_weak = true`; пригадане слово не слабке |
| AC-09 сортування | сортування `recent` / `alpha` / `weakest` стабільне між сторінками | unit + integration + component | [T03](tasks/T03-stats-controller-and-request.md), [T04](tasks/T04-ui-my-words-screen.md) | порядок відповідає правилу й стабільний при однакових лічильниках |
| AC-10 пошук | пошук за лемою або перекладом ігнорує діакритику; порожній результат — порожній список | unit + integration + component | [T03](tasks/T03-stats-controller-and-request.md), [T04](tasks/T04-ui-my-words-screen.md) | знайдено слова зі згорнутою формою запиту; порожній результат не є помилкою |
| AC-11 пагінація курсором | курсор `after` / `before` за `entry_id` не дублює й не губить рядків | integration | [T03](tasks/T03-stats-controller-and-request.md), [T05](tasks/T05-stats-integration-tests.md) | `LearnedWordsPage` з `has_next` / `has_prev` / `next_cursor`; обидва курсори разом відхилено |
| AC-12 поля з опублікованої версії | лема, транскрипція й переклади беруться з опублікованої версії, канонічний переклад першим | integration | [T02](tasks/T02-stats-service-counting.md), [T05](tasks/T05-stats-integration-tests.md) | знятий з публікації запис дає порожню транскрипцію; слово лишається з накопиченими лічильниками |
| AC-13 невалідний запит і онбординг | невідома вкладка/сортування, зайвий параметр, обидва курсори, не-uuid чи неонбордений learner | unit + integration | [T03](tasks/T03-stats-controller-and-request.md), [T05](tasks/T05-stats-integration-tests.md) | `400 stats.invalid_request`; неонбордений — `404 learner.not_onboarded`; чужа ідентичність — `401` |

**Контракт трьох операцій.** Одна contract-перевірка проганяє `listLearnedWords`, `getLearnedWordCard` і `getLearnedWordsSummary` з [openapi.yaml](./contracts/openapi.yaml) і перевіряє, що успішні й помилкові відповіді (конверт `{code, message, details?}`) збігаються зі схемами; клієнт Mini App компілюється з типами, згенерованими з того самого файлу ([T04](tasks/T04-ui-my-words-screen.md)).

## Edge cases / error paths

Кожен error- і authorization-AC має власний рядок вище: AC-06, AC-13. Додаткові випадки, які випливають з [idea-brief §9](idea-brief.md#9-trade-offs-and-edge-cases) і контракту:

- Слово без жодної відповіді (учень позначив «Вже знаю»): у «На повторення» з нулем відповідей, не слабке ([T02](tasks/T02-stats-service-counting.md), integration).
- Невідома вкладка/сортування, зайвий параметр, обидва курсори разом чи курсор не-uuid → `stats.invalid_request`, нічого не прочитано ([T03](tasks/T03-stats-controller-and-request.md), integration).
- Запис змінили, зняли з публікації або відкотили → список і зведення не розходяться зі «Сьогодні» ([T02](tasks/T02-stats-service-counting.md), integration).
- Зміна часового поясу зсуває дати першого, останнього й наступного повторення ([T02](tasks/T02-stats-service-counting.md), integration).
- Довгий список (сотні слів): порожній стан, підвантаження порціями, стабільний порядок при сортуванні за лічильниками, що змінюються ([T03](tasks/T03-stats-controller-and-request.md), integration; [T04](tasks/T04-ui-my-words-screen.md), component).
- Новий учень без жодного слова → порожня сторінка з поясненням ([T04](tasks/T04-ui-my-words-screen.md), component).
- Застарілий пошуковий запит при швидкому введенні → результат попереднього запиту не перезаписує новий ([T04](tasks/T04-ui-my-words-screen.md), component).

## Test data

- **Seed strategy:** фабрики з [personal-trainer-mvp data-model.md](../personal-trainer-mvp/data-model.md#test-fixtures) — `LearnerFixtures.NewLearner`, `ContentFixtures.NewPublishedEntry`, `LearningFixtures.NewActiveSession`, журнал `answer_events` і `answer_neutralizations`; для пагінації й сортування — learner з кількома сотнями слів зі змішаними станами й оскарженнями.
- **Час:** фіксований годинник у кожному тесті; сценарії дат повторень і зміни поясу задають момент явно. Абсолютна точка відліку — 2026-11-03.
- **Ідентичність:** тестовий підписувач `initData` з тестовим токеном бота; Telegram id з діапазону `900000000+`, ім'я `Test User` — без реальних персональних даних.
- **Integration dependency:** ефемерний PostgreSQL 16 у контейнері (Testcontainers) із застосованими міграціями — без підміни сховища в пам'яті; фіча таблиць не додає.
- **Cleanup boundary:** контейнер на набір тестів, стан очищується перед кожним тестом (ті самі таблиці, що читає статистика й очищає reset-progress).

## NFR validation

Латентність із [PRD §6](PRD.md#6-non-functional-requirements) перевіряється серверною метрикою в тому самому integration-прогоні — окремого навантажувального інструмента фіча не вводить (2 000 слів і до 20 учнів, продуктивність не вузьке місце).

- **Latency p95 списку ≤ 600 ms** → серверна метрика `GET /api/v1/stats/words` на learner з кількома сотнями слів.
- **Latency p95 зведення ≤ 400 ms** → серверна метрика `GET /api/v1/stats/summary`.
- **Latency p95 картки ≤ 500 ms** → серверна метрика `GET /api/v1/stats/words/{entry_id}`.
- **Узгодженість зі «Сьогодні»** → integration-тест спільного джерела `learner_words.status` (AC-04): 0 розходжень `recalled_count`.

## CI placement

- **На кожен PR у backend CI:** unit, integration на ефемерному PostgreSQL (пропуск через відсутність контейнера в CI вважається падінням), серверні contract-перевірки трьох операцій.
- **Локально за DoD задач Mini App (до CI не додаємо):** build, lint, typecheck і component (vitest); за рішенням від 2026-11-01 ([T04](tasks/T04-ui-my-words-screen.md)), як і для unlimited-learning та personal-trainer-mvp.
- **Вручну перед пілотом:** один smoke test у справжньому Telegram-клієнті з тестовим ботом і HTTPS-тунелем — відкрити «Мої слова», перевірити вкладки, пошук і запуск тренування слабких слів.
