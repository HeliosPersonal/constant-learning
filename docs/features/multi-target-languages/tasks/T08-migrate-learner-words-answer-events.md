---
id: T08
title: "Міграції дискримінатора learner_words + answer_events"
layer: "migration"
deps: ["[T05](./T05-migrate-content-entries.md)"]
acs: ["AC-04", "AC-05", "AC-10"]
files_hint:
  - "ConstantLearning/Migrations/"
  - "ConstantLearning/Features/Learning/Data/"
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "done"
---

# T08 — Міграції дискримінатора `learner_words` + `answer_events`

**Шар:** migration · **Залежить від:** [T05](./T05-migrate-content-entries.md) · **AC:** AC-04, AC-05, AC-10 · **Оцінка:** M · **Фаза:** 4 (Learning)

## Why

Ключ ізоляції §8 — `(learner_id, target_language)`; гарячі лічильники (слова до повторення,
ліміт «Вже знаю» на добу) мають фільтрувати за мовою **без join** (quality goal №3,
[ADR-0001](../adr/0001-use-single-learner-with-target-language-column.md),
[data-model.md §Altered entities](../data-model.md)). Денормалізація `target_language` — свідома.

## What

Перенести стейджені пари (кроки 13–21, [data-model.md §Міграційний план](../data-model.md)):

- `learner_words`: ADD nullable + inline FK + backfill з `dictionary_entries.target_language` через `entry_id` → `SET NOT NULL`.
- `index_learner_words_target_language` (FK), `index_learner_words_learner_lang_due`, `index_learner_words_learner_lang_status` — усі `CREATE INDEX CONCURRENTLY`.
- `answer_events`: ADD nullable + inline FK + backfill через `entry_id` → `SET NOT NULL`.
- `index_answer_events_target_language` (FK), `index_answer_events_learner_lang_day_outcome` — `CONCURRENTLY`.
- EF-сутності: `LearnerWord.TargetLanguage`, `AnswerEvent.TargetLanguage` + мапінг.

Старі не-мово-свідомі індекси лишаються additively (чистка — contract-фаза, [data-model.md §Борг](../data-model.md)).

## Definition of Done

- [x] Усі 9 кроків застосовано на testcontainer; кожен `CONCURRENTLY` — один statement (`suppressTransaction: true`).
- [x] Backfill виводить `target_language` з `entry_id`; `SET NOT NULL` проходить; `.down` реверсує.
- [x] Кожен FK має індекс (self-check FK зелений); мово-свідомі композитні індекси присутні.
- [x] Немає `CHECK`/`TRIGGER`/бізнес-`DEFAULT`.
- [x] PR ≤500 LOC.

## Нотатки реалізації

- Одна EF-міграція `20261104205156_MigrateLearnerProgressTargetLanguage` покриває обидві таблиці
  (прецедент `PersistSessionResultView` — транзакційні + `CONCURRENTLY` statement-и в одному файлі).
- FK додано як `ADD CONSTRAINT ... NOT VALID` + `VALIDATE CONSTRAINT` (не inline), щоб не тримати
  `ACCESS EXCLUSIVE` під час перевірки наявних рядків — прецедент
  [T03 `AddActiveTargetLanguageToLearners`](../../../../ConstantLearning/Migrations/20261104201539_AddActiveTargetLanguageToLearners.cs).
- Інтеримний C#-дефолт `TargetLanguage = "pl"` у `LearnerWord`/`AnswerEvent` з `TODO(T10)`: місця
  створення почнуть писати мову з активного контексту в [T10](./T10-progress-isolation.md).
- Міграційний тест `LearnerProgressTargetLanguageMigrationTests` ([DockerFact]) вставляє запис
  мовою `en` і доводить, що беклфіл виводить мову з `entry_id`, а не зашиває `'pl'`.
