---
id: T09
title: "Міграції дискримінатора learning_sessions + content_reports"
layer: "migration"
deps: ["[T05](./T05-migrate-content-entries.md)", "[T08](./T08-migrate-learner-words-answer-events.md)"]
acs: ["AC-04", "AC-09", "AC-11"]
files_hint:
  - "ConstantLearning/Migrations/"
  - "ConstantLearning/Features/Learning/Data/"
  - "ConstantLearning/Features/Content/Data/"
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T09 — Міграції дискримінатора `learning_sessions` + `content_reports`

**Шар:** migration · **Залежить від:** [T05](./T05-migrate-content-entries.md), [T08](./T08-migrate-learner-words-answer-events.md) · **AC:** AC-04, AC-09, AC-11 · **Оцінка:** M · **Фаза:** 4 (Learning)

## Why

Session належить одній мові й не досяжна через `entry` інакше (AC-04); черга content reports
відокремлюється за мовою (AC-09/AC-11). Обидві таблиці дістають денормалізований `target_language`
([data-model.md §Altered entities](../data-model.md)). `session_items` колонки не отримує — мова
досяжна через `session_id`.

## What

Перенести стейджені пари (кроки 22–29, [data-model.md §Міграційний план](../data-model.md)):

- `learning_sessions`: ADD nullable + inline FK + backfill `'pl'` → `SET NOT NULL`.
- `index_learning_sessions_target_language` (FK), `index_learning_sessions_learner_lang_active` partial `WHERE status='active'` — `CONCURRENTLY`.
- `content_reports`: ADD nullable + inline FK + backfill `'pl'` → `SET NOT NULL`.
- `index_content_reports_target_language` (FK), `index_content_reports_lang_status_created` — `CONCURRENTLY`.
- EF-сутності: `LearningSession.TargetLanguage`, `ContentReport.TargetLanguage` + мапінг.

## Definition of Done

- [ ] Усі 8 кроків застосовано на testcontainer; кожен `CONCURRENTLY` — один statement.
- [ ] Backfill → `'pl'`; `SET NOT NULL` проходить; `.down` реверсує всі кроки.
- [ ] FK-індекси + мово-свідомі композитні/partial індекси присутні (self-check FK зелений).
- [ ] `index_content_reports_lang_status_created` обслуговує чергу редактора за мовою (flow 7).
- [ ] PR ≤500 LOC.
