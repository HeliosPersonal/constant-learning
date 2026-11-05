# API sync report — multi-target-languages

- **Stage:** 10 (`sdlc:api-forge`)
- **Date:** 2026-11-04
- **Scenario:** **A** (data-model.md присутній → типізований контракт, походження полів з DDL).
- **Contract:** [`openapi.yaml`](./openapi.yaml) `info.version: 0.1.0`, OpenAPI 3.1.0.
- **Feature size:** M.

## Inputs detected

| Input | Status | Внесок |
|---|---|---|
| `PRD.md` | ✅ знайдено | US-01..US-07, AC-01..AC-12 → перелік ендпоінтів і гілки помилок |
| `data-model.md` | ✅ знайдено | `target_languages`, `learner_language_states`, `learners.active_target_language` → типи й обмеження полів (scenario A) |
| `sad.md` §6 | ✅ знайдено | 7 критичних flow (container-рівневі); alt/else гілки flow 3/4/6/7 → відповіді 409/403 |
| `adr/0001..0004` | ✅ знайдено | ADR-0001 (дискримінатор), 0002 (стан доби), 0003 (мова на записі), 0004 (конфіг-таблиця) — форма ресурсів |
| `idea-brief.md` | ✅ знайдено | Approach C → `info.description` |
| reference `personal-trainer-mvp/contracts/openapi.yaml` | ✅ знайдено | успадковано TelegramInitData (ADR-0005), конверт помилки, курсорна пагінація, EditorQueue-схеми |

**Style:** HTTP REST (OpenAPI 3.1) — ресурсна поверхня, той самий Mini App-споживач, що й reference.

## Deviations from course defaults (`.claude/rules/openapi.md`)

| Дефолт | Відхилення | Підстава |
|---|---|---|
| BearerAuth глобально | **TelegramInitData** (apiKey, header `Authorization: tma <initData>`) | reference ADR-0005 — ідентичність лише з підписаних initData; фіча успадковує, не вводить нову auth-модель |
| Списки — курсорна пагінація | `GET /languages` **без пагінації** (повний список) | набір мов обмежений (≈4, ADR-0004); курсор на 4 рядки — зайва складність. Черга редактора пагінацію зберігає |
| — | жодного `Idempotency-Key` заголовка | мутації ідемпотентні за природним ключем `(learner, target_language)` (AC-12, flow 4) — той самий патерн, що в reference (startSession, markAlreadyKnown); окремий заголовок не потрібен |

## Section A — Field origins (scenario A: усі high)

| schema.field | origin | confidence |
|---|---|---|
| `TargetLanguage.code` | `target_languages.code` VARCHAR(2) → `pattern ^[a-z]{2}$`, maxLength 2 | high |
| `TargetLanguage.display_name_uk` | `target_languages.display_name_uk` VARCHAR(64) → maxLength 64 | high |
| `TargetLanguage.is_enabled` | `target_languages.is_enabled` BOOLEAN | high |
| `TargetLanguage.sort_order` | `target_languages.sort_order` SMALLINT → integer 0..32767 | high |
| `TargetLanguage.available` | обчислюване: `is_enabled` ∧ наявний approved-контент (AC-02, flow 3) | high (derived) |
| `LearnerLanguage.target_language` | `learner_language_states.target_language` FK → `target_languages.code` | high |
| `LearnerLanguage.display_name_uk` | `target_languages.display_name_uk` VARCHAR(64) (join за `target_language`) | high |
| `LearnerLanguage.started_at` | `learner_language_states.created_at` TIMESTAMPTZ | high |
| `LearnerLanguage.is_active` | `learners.active_target_language` = рядок (ADR-0001) | high (derived) |
| `LearnerLanguages.active_target_language` | `learners.active_target_language` VARCHAR(2) NULL | high |
| `StartLanguageRequest.target_language` / `SetActiveLanguageRequest.target_language` | вхід → `learner_language_states.target_language` / `learners.active_target_language` | high |
| `ActiveLanguage.active_target_language` | `learners.active_target_language` | high |
| `EditorQueueItem.target_language` | `content_reports.target_language` VARCHAR(2) NOT NULL (data-model.md §ALTER) | high |
| `EditorQueueItem.lemma` | `entry_versions.lemma_pl` → мово-нейтральне ім'я (data-model.md: «лема активної мови»; перейменування колонки — борг SAD §11) | high |
| решта `EditorQueueItem.*` | reference EditorQueueItem (успадковано без змін) | high |

## Section B — Drift findings (5-point)

1. **Endpoint ↔ data-model — ✅.** Кожен ендпоінт відображається на запит/мутацію сутності:
   - `listTargetLanguages` → `SELECT target_languages ORDER BY sort_order` (+ EXISTS approved `dictionary_entries` для `available`).
   - `listLearnerLanguages` → `SELECT learner_language_states WHERE learner_id=?` + `learners.active_target_language`.
   - `startLearnerLanguage` → `INSERT learner_language_states` + `UPDATE learners.active_target_language` (під lock рядка (learner, lang), ADR-0002).
   - `setActiveLanguage` → `UPDATE learners.active_target_language`.
   - `listEditorQueueByLanguage` → `SELECT content_reports WHERE target_language=? ORDER BY created_at` (індекс `ix_content_reports_lang_status_created`, data-model.md).

2. **Error codes ↔ domain sentinels — ✅ (нові коди, реєстр поповнюється на implement-tasks).** Нові коди фічі: `language.unknown`, `language.not_available`, `language.not_studying`, `language.concurrent_update`. Решта (`auth.invalid_init_data`, `learner.not_onboarded`, `editor.role_required`, `editor.invalid_request`) — наявні sentinel-и reference, рядок незмінний. Кожен `code` відповідає `^[a-z_]+\.[a-z_]+$`; namespace `language.*` = модуль Identity. **Follow-up:** завести 4 нові константи в реєстрі помилок застосунку (той самий рядок) під час implement-tasks.

3. **Validation ↔ DB constraints — ✅.** `code` `pattern ^[a-z]{2}$` + maxLength 2 ↔ VARCHAR(2); `display_name_uk` maxLength 64 ↔ VARCHAR(64); `sort_order` 0..32767 ↔ SMALLINT; `lemma`/`*_value`/`comment`/`search_query` maxLength успадковані з reference DDL (`entry_versions`, `content_reports`).

4. **Entity ↔ endpoint — ✅ з явними нотатками.** `target_languages`, `learner_language_states`, `learners.active_target_language`, `content_reports.target_language` — усі обслуговуються. **Навмисно не в цьому контракті (крос-файлова дельта, point 5 нижче):** `target_language` на `dictionary_entries` / `learner_words` / `answer_events` / `learning_sessions` обслуговуються наявними reference-ендпоінтами (today, sessions, dictionary) після серверного active-language scoping — DDL-дискримінатори, а не нова HTTP-поверхня. `learner_language_states.current_day_*`, `daily_new_words_limit` — внутрішній стан доби, читається сервером при оцінці session (reference /sessions), не окремим ендпоінтом.

5. **OpenAPI ↔ sequence — ✅.** Методи/коди узгоджені з flow §6:
   - flow 1 (AC-03/AC-07) → `setActiveLanguage` 200 + подальший reference `getToday`.
   - flow 3 (AC-02) → `startLearnerLanguage` 409 `language.not_available`.
   - flow 4 (AC-12/OQ-4) → `startLearnerLanguage` 200 (ідемпотентно) / 409 `language.concurrent_update`.
   - flow 7 (AC-11) → `listEditorQueueByLanguage` з `target_language`, елементи несуть мову.
   - **Крос-файлова дельта (зливається в reference-контракт на інтеграції, не редагується тут — invariant «Never edit sources»):** `Learner.active_target_language` (additive, nullable), `TodaySummary.target_language` (additive), прив'язка `createContentReport` до мови запису (сервер виводить `target_language` з `entry_id`, тіло запиту не змінюється). Версію reference-контракту підняти при злитті.
   - **Узгодження черги редактора при злитті:** `listEditorQueueByLanguage` тут — надмножина reference-операції `listEditorQueue` (додано query-параметр `target_language` + поле `EditorQueueItem.target_language`, а `lemma_pl` подано як мово-нейтральне `lemma`). На інтеграції це **не друга операція**, а розширення наявної: reference `listEditorQueue` оновлюється до цієї форми (той самий `operationId`/шлях), а не дублюється. Перейменування колонки `lemma_pl → lemma` — борг SAD §11 (нижче).

**Core checks (1, 2, 3): усі ✅.** Supporting checks (4, 5): ✅ з задокументованими крос-файловими дельтами.

> **T13 (2026-11-05):** серверне active-language scoping наявних ендпоінтів реалізовано й покрито
> contract-тестами (`ContractAssert` на `setActiveLanguage`, `searchDictionary`, `getDictionaryEntry`,
> `startSession`, `getCurrentSession`, `acknowledgeNewWordCard`). Today/session/settings/profile вже
> скоуплено через `LearnerDayStateService.GetForUpdateAsync` (T10); дельта point 4 для **словника**
> (`DictionarySearchService` — пошук + картка за `learners.active_target_language`) дописана в T13.
> Крос-файлові дельти point 5 лишаються незмінними — зливаються в reference-контракт на інтеграції.

## Section C — unresolved_origins

**Порожньо** (scenario A). Усі поля мають походження в типізованій сутності data-model.md.

## Open items that touch the contract — закрито (2026-11-04)

- **OQ-5 RESOLVED:** `normalization_profile` для de/nl заводиться в spike-задачі T07s (`'de'`/`'nl'`), правила кодуються в T07a/T07b. Контракт профіль **не експонує** (внутрішнє рішення коду) — рішення на поверхню API не впливає; `available=true` для de/nl настане з наявністю контенту (OQ-7 spike T07s).
- **OQ-4 RESOLVED:** конкурентність — **оптимістична через `xmin`** `(learner_id, target_language)`; `409 language.concurrent_update` = клієнт повторює (без server-side retry). Контракт уже має цей код — змін не потребує.
- **OQ-9 RESOLVED:** роль content editor **єдина для всіх мов** — `listEditorQueueByLanguage` **без per-language authz** (жодної перевірки «редактор саме цієї мови»). Загальний `403 editor.role_required` за роллю **лишається** в контракті — його успадковано від reference EditorQueue й він перевіряє наявність ролі, а не мову.
- **Лишається — борг SAD §11:** перейменування `lemma_pl` → `lemma`. Контракт уже вживає мово-нейтральне `lemma` в `EditorQueueItem`; злиття з reference вимагатиме узгодження імені поля.

## Integration merge into reference contract (T15, 2026-11-05)

T15 — перший споживач мовних ендпоінтів у SPA; SPA генерує типи (`ConstantLearning.MiniApp/src/api/schema.d.ts`)
з **reference-контракту** `docs/features/personal-trainer-mvp/contracts/openapi.yaml`, тож дельту цієї фічі
злито туди (крок, передбачений вище: «зливається в reference-контракт на інтеграції»). Виконано:

- **Версія reference-контракту:** `0.5.0` → `0.6.0` (з явним записом у CHANGELOG — за правилом «info.version bumps only with explicit CHANGELOG line»).
- **Мовні шляхи (additive):** `GET /api/v1/languages` (`listTargetLanguages`), `GET`+`POST /api/v1/learners/me/languages` (`listLearnerLanguages`/`startLearnerLanguage`), `PUT /api/v1/learners/me/active-language` (`setActiveLanguage`) — скопійовано verbatim із фіча-контракту; тег `languages`.
- **Схеми (8):** `TargetLanguageCode`, `TargetLanguage`, `TargetLanguageCatalog`, `LearnerLanguage`, `LearnerLanguages`, `StartLanguageRequest`, `SetActiveLanguageRequest`, `ActiveLanguage`.
- **Телеметрія:** `ClientTiming.metric` enum `[today_ready_ms]` → `[today_ready_ms, language_switch_ms]` (окрема гістограма `trainer_language_switch_ms`, NFR ≤1500 ms). Бек: `TrainerMetrics.RecordLanguageSwitch` + маршрутизація в `ClientTimingController`.
- **Валідація:** backend `ContractAssert` у тестах валідує відповіді проти reference-контракту; телеметричний тест (`ReportClientTiming_RecordsLanguageSwitchMetricSeparately`) проходить після злиття.

## Integration merge into reference contract (T16, 2026-11-05)

T16 — SPA-споживач черги редактора; злиття черги редактора у reference-контракт
(`docs/features/personal-trainer-mvp/contracts/openapi.yaml`), раніше відкладене з T15. Виконано:

- **`GET /api/v1/editor/content-reports`:** `operationId` `listEditorQueue` → `listEditorQueueByLanguage`;
  додано перший query-параметр `target_language` (`$ref: TargetLanguageCode`, необов'язковий — без нього черга повертає всі мови); оновлено summary/description (per-language черга, AC-11).
- **`EditorQueueItem`:** додано `target_language` (`$ref: TargetLanguageCode`, у `required`); колонку
  `lemma_pl` перейменовано на мово-нейтральну `lemma` (узгоджено борг SAD §11); приклад `items[0]` оновлено (`target_language: pl`, `lemma: urząd`).
- **`ReportField` — без змін:** бекенд (T14 `EditorQueueContracts.cs` + `ContentReportRequest.cs`) уже вживає
  `[lemma, translation, pronunciation, example]`, що збігається з reference-`ReportField`; дивергентний
  перелік фіча-контракту (`[translation, transcription, example, lemma, other]`) був doc-stage здогадкою, якої
  бекенд не реалізував — тож міняти reference-enum не потрібно (бекенд — джерело істини). `EditorQueuePage` успадковано без змін.
- **Валідація без spectral:** регенерація `openapi-typescript` (`npm run generate:api`) + SPA `tsc --noEmit` чисто
  + `vitest run` 281/281 + backend `ContractAssert` слугують де-факто перевіркою контракту (spectral/npx недоступні в середовищі).
- **Борг SAD §11 `lemma_pl` → `lemma`:** закрито в межах `EditorQueueItem`; у `AppliedChange` поле `lemma_pl` лишається
  (інша схема, окремий контракт — поза межами T16).

## Lint / mock

- `spectral lint docs/features/multi-target-languages/contracts/openapi.yaml` — **не виконано в цьому середовищі** (немає Node/Python на PATH). Проведено ручну перевірку: усі 12 `$ref` схем і 3 `$ref` responses резолвляться; 0 заборонених токенів (`nullable: true`, `additionalProperties: true` на відповідях); 5 унікальних `operationId`; конверт помилки `{code, message, details?}` на кожному 4xx. **Запустити `spectral lint` у середовищі з Node перед merge** (той самий gate, що проходив reference-контракт).
- Mock: `prism mock docs/features/multi-target-languages/contracts/openapi.yaml` — підняти для FE-інтеграції мовного перемикача.

## Self-check (DoD)

- ✅ Контракт у `contracts/openapi.yaml`, scenario A зафіксовано.
- ✅ `api-sync-report.md` поруч; core checks 1–3 усі ✅; `unresolved_origins` порожній (scenario A).
- ✅ Приклади на кожній операції (request + 2xx + приклади помилок); PII — плейсхолдери (`Test User`, `example.test`).
- ✅ Конверт помилки з кодами `module.error_name`; HTTP-класи 400/401/403/404/409.
- ⚠️ `spectral lint` — відкладено до середовища з Node (ручна перевірка пройдена).
- ⚠️ Mock-сервер — запустити при старті FE-інтеграції.

## Next stage

`sdlc:plan-tests multi-target-languages`, далі `sdlc:break-tasks`.
