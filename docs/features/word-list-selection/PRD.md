---
status: Draft
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead", "Security Lead"]
updated_at: "2026-11-06"
feature_size: L
stage: "03"
ticket: "TBD"
---

# PRD — word-list-selection

> **Inputs (required):** [idea-brief](./idea-brief.md) · [CONTEXT](../../../CONTEXT.md)
> **Reference modules:** `multi-target-languages` (languages-as-data, `target_languages`, `learner_language_states`, `validation_status`, `published_version_id`, selection lives in Settings) · `personal-trainer-mvp` (progress-on-entry: `learner_words` UNIQUE `learner_id`+`entry_id`; stable `dictionary_entries.id`/`external_id`; `frequency_rank`/`utility_rank`; `domain` everyday/legalization) · bot code `ConstantLearning/` (C#/EF Core; `SessionComposer` domain rotation; error envelope `{code, message}` with `module.error_name`).
> **External context channels used:** None — only CONTEXT + idea-brief + the three reference modules above.

## 1. Context

Сьогодні джерело нових слів для learner неявно фіксоване: усі починають з того самого частотного набору в єдиному порядку, а єдиний вибір — `domain` (everyday / legalization). Learner не може ні звузити навчання під свою мету, ні взяти структурований рівневий набір (A1→C2) — бо такого контенту просто не існує. Корінь обох болів один: у моделі немає рівневого виміру контенту й немає механіки обрати, звідки беруться нові слова. Сегмент, що страждає найбільше, — рівне-свідомі learner, яким загальний частотний потік дає забагато нерелевантних слів і провокує «вже знаю» та відмови (idea-brief §2, §3).

Чому зараз: це природне продовження multi-target-languages, яка ввела base course на мову, каталог мов як дані (`target_languages`) і перемикання активної мови. Інфраструктура «набір як дані + скоупінг за мовою» вже в проді; додати вимір «який саме набір у межах мови» — наступний крок. Пара польська↔українська — незайнята ніша: готового CEFR-набору польської для української аудиторії ніхто не постачає (idea-brief §4).

Прийнятий вектор — Approach C (idea-brief §13). Двофазна фіча: (1) контентний конвеєр будує CEFR-набори — частотний кістяк беремо з пермісивних корпусів, модель ШІ лише призначає рівень і заповнює переклади/приклади (grounded, не вигадує кістяк), а якість стереже багаторівнева багатоагентна AI-перевірка плюс випадковий людський аудит ~30 слів на набір; (2) механіка вибору виставляє списки learner з дефолтом {наш курс + легалізація}.

Traceability (reference-патерни, які фіча успадковує — не AC): прогрес ведеться на `entry` (`learner_words` UNIQUE `learner_id`+`entry_id`), тож перетин списків дедуплікується безплатно. Стабільний `dictionary_entries.id` / `external_id` — ідемпотентний ключ імпорту, тож повторний прогін конвеєра не осиротить прогрес уже виданих слів. Вибір джерела — server-side state на learner, живе у Settings (як перемикання активної мови). Конверт помилок — `{code, message}` з `module.error_name` (інлайн-літерали per feature, без центрального реєстру). **Застереження з топ-ризику (idea-brief §10):** Approach C впускає AI-походжений контент у словник, який досі гарантував «approved = доказово підтверджено» — свідоме послаблення ядра, що вимагає ADR, інакше downstream-запобіжники (публікація, report-черга, auto-verify) довірятимуть неперевіреному наосліп.

**Panel decisions (expert panel, 2026-11-06 — закривають idea-brief §15, формалізуються як ADR на architecture-design):**

- **D1 (OQ1, карбаут approved — одностайно).** AI-origin контент позначається окремим полем `entry.origin ∈ {evidence, ai}`, ортогональним до `validation_status`; entry з `origin=ai` не може набути `validation_status='approved'` без human sign-off, а publish-time gate блокує неузгоджений або license-tainted origin. Це зберігає load-bearing значення `validation_status='approved'`, на яке спираються всі downstream guards, і дає per-entry provenance для license-debt трасування.
- **D2 (OQ2, вісь CEFR — одностайно).** CEFR level зберігається як versioned per-entry атрибут `entry_versions.cefr_level` (несе власний origin/evidence, бо рівень польської — суб'єктивна editorial/AI-claim); CEFR-набори та base course — це запити над `dictionary_entries`, а не окремі membership-сутності. Дзеркалить наявний патерн base course і зберігає dedupe-on-entry та відтворюваність pipeline по стабільному id.
- **D3 (OQ3, перетин/порядок — 4:1).** Перетин списків вирішується list rotation: overlapping word береться один раз у порядку першого обраного списку, в якому він з'являється; вибірка резолвиться одним indexed-запитом (партиційний індекс `target_language`, `course_position`), а curated-порядок усередині CEFR-набору зберігається (рівні не interleave-яться всередині одного CEFR-ладдера).
- **D4 (OQ4, гейт якості — одностайно).** Quality gate = багаторівнева багатоагентна AI-перевірка як первинний фільтр + людський аудит ~30 слів/набір як **калібратор** (не фільтр) + обов'язкове per-entry provenance tracing; супроводжується per-entry provenance та audit-outcome телеметрією для вимірюваного порогу false-entry-rate.

## 2. Goals

- Learner обирає, з яких списків беруться нові слова (один чи кілька), прямо в налаштуваннях — без зміни денного ліміту чи логіки повторень (idea-brief §13).
- Рівне-свідомий learner отримує структурований рівневий CEFR-набір замість загального частотного потоку (idea-brief §13).
- Перетин списків не дублює й не осиротює прогрес — те саме слово з двох списків показується один раз (idea-brief §8 Engineer).
- Дефолт {наш курс + легалізація} зберігає активацію — фіча не додає обов'язкового кроку на старті (idea-brief §8 UX).

## 3. Non-goals

- Персональні (user-generated) списки слів — out of scope, перша ітерація лише куровані набори (idea-brief §6).
- Автовизначення стартового рівня / самооцінка learner на старті — out, усі починають без розподілу за рівнями (idea-brief §6; CONTEXT Out of scope).
- Нецільові мови — перша ітерація лише польська для україномовних (idea-brief §6).
- Зміна денного ліміту нових слів або логіки повторень — out, механіка вибору лише задає джерело (idea-brief §6).
- Повна CEFR-драбина одразу «на продакшн» — контент будується й валідується пілотним рівнем уперед, не всі шість рівнів наосліп (idea-brief §6).

## 4. User stories

### US-01: Обрати джерела нових слів

**As a** learner
**I want** обрати один або кілька списків слів у налаштуваннях
**So that** нові слова беруться саме з того, що відповідає моїй меті

### US-02: Переглянути доступні списки

**As a** learner
**I want** бачити доступні списки з їх рівнем / міткою й кількістю слів
**So that** я обираю свідомо, а не навмання

### US-03: Почати з дефолту

**As a** learner
**I want** почати навчання без обов'язкового вибору списків
**So that** старт не має зайвого кроку (дефолт: {наш курс + легалізація})

### US-04: Зняти список без втрати прогресу

**As a** learner
**I want** зняти список посеред курсу
**So that** добір нових слів з нього спиняється, а вивчене й повтори лишаються

### US-05: Не отримувати дублів з перетину списків

**As a** learner
**I want** щоб слово, яке є в кількох обраних списках, показувалося один раз
**So that** я не марную час на дубль того самого слова

### US-06: Не лишитися без джерела нових слів

**As a** learner
**I want** щоб система не дала мені лишити навчання без джерела нових слів
**So that** добір нового ніколи не зупиняється випадково

### US-07: Керувати видимістю набору

**As a** content editor
**I want** керувати тим, чи набір видимий і вибираний для learner (публікація / зняття як джерела)
**So that** learner бачать у виборі лише перевірені, опубліковані набори

### US-08: Відрізняти AI-походжений контент

**As a** content editor
**I want** щоб AI-походжені записи були позначені окремо від доказово-approved
**So that** downstream-запобіжники не довіряли їм наосліп (карбаут, idea-brief §10)

### US-09: Бачити джерело слова

**As a** learner
**I want** бачити маленький ненав'язливий маркер, з якого набору походить слово
**So that** я розумію джерело, не відволікаючись від вправи

## 5. Acceptance criteria

### AC-01 (US-01) — happy path

**Given** learner з активною цільовою мовою та доступними опублікованими списками
**When** learner обирає один або кілька списків як джерело нових слів
**Then** система зберігає вибір, і наступні нові слова беруться лише з обраних списків, підтверджуючи вибір learner

### AC-02 (US-03) — happy path (дефолт)

**Given** новий learner, який не робив явного вибору списків
**When** learner починає заняття
**Then** система подає нові слова з дефолтного джерела {наш курс + легалізація} без додаткового кроку вибору

### AC-03 (US-02) — happy path (перегляд)

**Given** learner відкриває екран вибору списків
**When** система показує доступні списки
**Then** кожен список показано з його рівнем / міткою та кількістю слів, і видимими є лише опубліковані (перевірені) списки

### AC-04 (US-05) — domain invariant (дедуп перетину)

**Given** слово належить до двох обраних learner списків
**When** система добирає нові слова, чергуючи між обраними списками
**Then** слово подається рівно один раз — за порядком першого обраного списку, в якому воно трапляється; облік чергування це враховує

### AC-05 (US-04) — domain invariant (зняття зберігає прогрес)

**Given** learner має вивчені слова й заплановані повтори зі списку
**When** learner знімає цей список посеред курсу
**Then** система спиняє лише добір нових слів з нього, а вивчене й повтори лишаються недоторканими

### AC-06 (US-06) — error (останній список)

**Given** learner має обраним лише один список
**When** learner намагається зняти цей останній список
**Then** система блокує зняття й пояснює learner, що має лишатися хоча б одне джерело нових слів

### AC-07 (US-01) — error (недоступний список)

**Given** learner обирає список, знятий з публікації або без перевірених слів
**When** learner підтверджує вибір
**Then** система блокує вибір і пояснює learner, що список недоступний, лишаючи попередній вибір без змін

### AC-08 (US-07) — authorization

**Given** learner (не content editor)
**When** learner намагається зробити список видимим / доступним для вибору
**Then** система не дозволяє дію — керування видимістю списку доступне лише content editor; learner може лише обирати з уже опублікованих

### AC-09 (US-07, US-02) — cross-context

**Given** learner з активною цільовою мовою польська
**When** learner переглядає доступні списки
**Then** система пропонує лише списки тієї самої активної цільової мови; списки інших мов не показуються й не вибираються (вибір залежить від стану активної мови в контексті Identity)

### AC-10 (US-08) — domain invariant (карбаут approved)

**Given** набір містить і доказово-approved, і AI-походжені записи
**When** content editor переглядає склад набору
**Then** система показує AI-походження окремим маркером, і жоден downstream-запобіжник не трактує AI-походжений запис як доказово підтверджений — інваріант «approved означає доказово підтверджено»

### AC-11 (US-09) — happy path (маркер джерела)

**Given** слово належить до одного чи кількох наборів-джерел
**When** learner бачить картку нового слова або запис у «Мої слова»
**Then** система показує маленький ненав'язливий маркер набору(ів)-джерела, не привертаючи зайвої уваги до вправи

## 6. Non-functional requirements

| Aspect | Target | Measurement |
|---|---|---|
| Latency p95 оновлення вибору списків | ≤ 200 ms | метрика ендпоінта налаштувань learner |
| Latency p95 читання доступних списків | ≤ 250 ms | метрика ендпоінта списків |
| Latency p95 складання session з N обраними списками | ≤ 400 ms | метрика компонування session |
| Availability | 99.5% | місячне SLO-вікно |
| Коректність дедупу / чергування | 0 дублів на learner (інваріант) | isolation integration test + звірка `answer_events` |

- Rotation по N обраних списках (D3) **мусить** резолвитися одним indexed-запитом (партиційний індекс `target_language`, `course_position`), а не N round-trips, інакше бюджет ≤400 ms деградує з кількістю списків.
- Offline AI-pipeline (harvest, калібровка рівня, багатоагентна перевірка) — batch-процес поза цими NFR; його SLO визначає architecture-design.

## 6.1 Security / privacy

- **Data classification:** internal — вибір списків це навчальна перевага learner, не regulated.
- **Personal data touched:** новий per-learner запис вибору списків (preference, low sensitivity); нового PII немає.
- **AuthZ/AuthN impact:** вибір — server-side state на learner; repo завжди фільтрує за `learner_id` (+ `target_language`), ніколи за trusted client param. Керування видимістю набору — лише роль content editor.
- **Abuse cases:**
  - **Cross-learner tampering:** learner намагається читати / міняти вибір іншого → система резолвить до власного server-side стану, чужий — приховано (hide existence, не 403-формулювання).
  - **Draft/unpublished leak:** неопубліковані / draft набори не видимі learner — фільтр у repo, не в handler.
  - **AI-origin trust:** AI-походжений контент ніколи не трактується як доказово-approved (D1 окреме поле `origin` + ADR).
  - **License debt:** per-entry provenance tracing; publish-time gate блокує entry, чий provenance веде до share-alike / NonCommercial джерела (Wiktionary CC BY-SA, Kelly-list NC). PII-guard поширюється й на AI-gap-filled pl→uk переклади.
  - **Spam toggling:** rate limit на оновлення налаштувань вибору (напр. 30/min/learner).
- **Security review:** Required — новий authz boundary (content editor visibility) + карбаут approved зачіпає ядро контент-гарантії, + ліцензійний борг і AI-походжені переклади.

## 7. Metrics / KPIs

- **Adoption вибору списків** — baseline: 0, target: ≥40% активних learner зробили явний вибір за 30 днів. Потребує телеметрії події зміни вибору (інакше необчислюване).
- **Uptake CEFR-набору** — baseline: 0, target: ≥30% серед learner, які дійшли до surface вибору (не від усіх actives — дефолт {наш курс + легалізація} структурно тисне метрику вниз через онбординг-friction). Discoverability sub-goal: частка learner, які відкрили екран вибору (hidden vs explicit default).
- **«Вже знаю» rate** — baseline: 2 тижні виміру до релізу, target: ↓ у когорті з CEFR-набором.
- **Частка хибних записів (аудит ~30 слів/набір)** — baseline: TBD (перший аудит), target: ≤ редакційний поріг. Потребує per-entry provenance + audit-outcome телеметрії, інакше quality-обіцянка (D4) неверифіковна.

## 8. Open questions

- [ ] Формалізувати D1–D4 як ADR на architecture-design (карбаут `origin`, вісь `cefr_level`, list rotation, quality gate)? Default: рішення панелі 2026-11-06 (§1 ¶4). — owner: Tech Lead, due: architecture-design.
- [ ] Discoverability дефолту: прихований дефолт {наш курс + легалізація} vs явний крок на онбордингу — як показати фічу без удару по активації? Default: прихований дефолт + видимий вхід у Settings. — owner: PM, due: architecture-design.
- [ ] Дизайн телеметрії якості контенту: подія зміни вибору/active-source + per-entry false-entry-rate / audit-outcome метрика з порогом. — owner: PM + Tech Lead, due: architecture-design.
- [ ] Стандарт лематизації/дедуплікації між джерелами (форми, регістр, омоніми) — щоб перетин наборів не губив і не дублював слова поза дедупом-на-entry. — owner: Tech Lead, due: architecture-design.
