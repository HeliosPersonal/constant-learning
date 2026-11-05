---
id: T02
title: "Per-language фікстури й оснастка ізоляційних тестів"
layer: "tests"
deps: ["[T01](./T01-config-target-languages.md)"]
acs: ["AC-04", "AC-05", "AC-10"]
files_hint:
  - "ConstantLearning.Tests/Fixtures/"
  - "ConstantLearning.Tests/Integration/"
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "done"
---

# T02 — Per-language фікстури й оснастка ізоляційних тестів

**Шар:** tests · **Залежить від:** [T01](./T01-config-target-languages.md) · **AC:** AC-04, AC-05, AC-10 · **Оцінка:** M · **Фаза:** 1 (Фундамент)

## Why

[test-plan.md](../test-plan.md) робить ізоляцію за `(learner, мова)` ризиком №1 і вимагає
реальної БД (PostgreSQL 16 testcontainer, **без mock DB** — анти-патерн) та детермінованих
фікстур на кожну мову. Готова оснастка розблоковує DoD усіх наступних задач, бо кожна несе
власні integration-тести.

## What

- Фабрики у `ConstantLearning.Tests/Fixtures/` ([data-model.md §Test fixtures](../data-model.md)):
  - `LearnerFixtures.NewLearnerStudying(code, dailyLimit)` — learner із рядком
    `learner_language_states` і `active_target_language=code`;
  - `ContentFixtures.NewPublishedEntry(targetLanguage, lemma, …)` — опублікований запис мови з `course_position`;
  - `ContentFixtures.NewContentReport(learner, entry, targetLanguage, origin)`.
- Хелпери ізоляційного тесту: два learner / дві мови; підписувач test initData
  (Telegram id `900000000+`, `Test User` — PII guard); фіксований годинник (FakeClock).
- Прибирання per-test (truncate/respawn) — без flaky.

## Definition of Done

- [x] Фабрики компілюються й застосовані мінімум в одному smoke-тесті на кожну (learner, entry, report).
- [x] Integration-база — PostgreSQL 16 testcontainer; жодного мокнутого DbContext.
- [x] Фіксований годинник інжектується; тести детерміновані (повторний прогін — той самий результат).
- [x] PII guard: немає реальних імен/контактів; лише `Test User`, `*@example.test`, id `900000000+`.
- [x] PR ≤500 LOC.

## Нотатки реалізації

- **EF-фабрики, викликач персистить (прецедент T01 `TargetLanguageFixtures.NewEnabledLanguage`).** Нові
  per-language фабрики повертають справжні EF-сутності; транзакцію/порядок save тримає тест (важливо для
  `ILearnerDayStateService.GetForUpdateAsync`, якому потрібна відкрита транзакція). Без параметра `AppDbContext`.
- **Легасі DTO-фабрики лишилися.** `ContentFixtures.NewPublishedEntry(lemmaPl, …)`/`NewContentReport(LearnerData, …)`
  із `personal-trainer-mvp` досі вживають 6 тест-файлів — не чіпав. Нові EF-методи додані як перевантаження:
  `NewPublishedEntry(targetLanguage, lemmaPl, coursePosition, …)` розрізняється обовʼязковим `int coursePosition`,
  `NewContentReport(Learner, DictionaryEntry?, targetLanguage, …)` — типом першого аргументу (`Learner` vs DTO
  `LearnerData`). Збіг імен із [data-model.md §Test fixtures](../data-model.md) збережено.
- **`NewLearnerStudying` повертає пару `StudyingLearner(Learner, LearnerLanguageState)`** — навігації між ними
  немає, тож викликач додає обидві сутності. Рішення щодо форми (перевантаження vs рядок-пара) ухвалено
  голосуванням 3 агентів (одностайно EF+пара; 2/3 за перевантаження зі збереженням імен зі спеки).
- **Опублікований запис — дволанцюговий save.** Через цикл FK `dictionary_entries ⇄ entry_versions` фабрика
  НЕ виставляє `PublishedVersionId`; smoke-тест зберігає граф, лінкує published-версію, зберігає ще раз
  (ідіома repo `PublishedCourseLanguageTests`).
- **Smoke-тести** — `Fixtures/PerLanguageFixtureTests.cs` поверх `PostgresIntegrationTest` (реальний
  testcontainer, `FakeClock`): learner+state, published entry, content report, плюс ізоляція «два learner / дві мови».
  Коди мов (pl/en) переживають reset (seed `target_languages`) — фабрики ними лише користуються.
