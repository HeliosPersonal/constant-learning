---
id: T03
title: "Міграції learner_language_states + learners.active_target_language"
layer: "migration"
deps: ["[T01](./T01-config-target-languages.md)"]
acs: ["AC-05", "AC-07"]
files_hint:
  - "ConstantLearning/Migrations/"
  - "ConstantLearning/Features/Identity/Data/"
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T03 — Міграції `learner_language_states` + `learners.active_target_language`

**Шар:** migration · **Залежить від:** [T01](./T01-config-target-languages.md) · **AC:** AC-05, AC-07 · **Оцінка:** M · **Фаза:** 2 (Identity)

## Why

Per-language стан доби потребує власної таблиці
([ADR-0002](../adr/0002-scope-day-state-per-target-language.md)), а вказівник активної мови —
колонки на `learners` ([ADR-0001](../adr/0001-use-single-learner-with-target-language-column.md)).
Облік видачі нових слів (AC-05) і порожній початковий стан (AC-07) стають незалежними на мову.

## What

Перенести стейджені пари (кроки 3–6, [data-model.md §Міграційний план](../data-model.md)) в EF-міграції:

- `create_learner_language_states` — складений PK (`learner_id`, `target_language`) + FK на
  `learners`/`target_languages` + FK-індекс `ix_learner_language_states_target_language` (нова таблиця → звичайний індекс).
- `backfill_learner_language_states_pl` — рядок `'pl'` на кожного learner зі знімка полів `learners`.
- `alter_learners_add_active_target_language` — ADD COLUMN nullable + inline FK + backfill `'pl'` наявним.
- `index_learners_active_target_language` — `CREATE INDEX CONCURRENTLY` (один statement, `suppressTransaction: true`).
- EF-сутність `LearnerLanguageState` + мапінг; `Learner.ActiveTargetLanguage` nullable.

Наявні `learners.current_day_*` / `daily_new_words_limit` / … **не видаляються** (contract-фаза — борг, [data-model.md §Борг](../data-model.md)).

## Definition of Done

- [ ] Усі 4 пари застосовано на testcontainer; `CONCURRENTLY`-файл — рівно один statement.
- [ ] Backfill дає рядок `'pl'` кожному наявному learner; `active_target_language='pl'` у наявних, NULL для нового learner.
- [ ] Кожен FK має індекс (self-check FK-indexes зелений); `.down` реверсує всі 4 кроки.
- [ ] Немає `CHECK`/`TRIGGER`/бізнес-`DEFAULT`; п'ять `current_day_*` nullable разом (інваріант — в app-коді, не DB).
- [ ] PR ≤500 LOC.
