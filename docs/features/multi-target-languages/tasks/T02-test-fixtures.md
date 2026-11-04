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
status: "todo"
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

- [ ] Фабрики компілюються й застосовані мінімум в одному smoke-тесті на кожну (learner, entry, report).
- [ ] Integration-база — PostgreSQL 16 testcontainer; жодного мокнутого DbContext.
- [ ] Фіксований годинник інжектується; тести детерміновані (повторний прогін — той самий результат).
- [ ] PII guard: немає реальних імен/контактів; лише `Test User`, `*@example.test`, id `900000000+`.
- [ ] PR ≤500 LOC.
