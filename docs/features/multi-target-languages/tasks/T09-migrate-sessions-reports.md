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
status: "done"
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

- [x] Усі кроки застосовано на testcontainer; кожен `CONCURRENTLY` — один statement.
- [x] Backfill → `'pl'`; `SET NOT NULL` проходить; `.down` реверсує всі кроки.
- [x] FK-індекси + мово-свідомі композитні/partial індекси присутні (self-check FK зелений).
- [x] `ix_content_reports_lang_status_created` обслуговує чергу редактора за мовою (flow 7).
- [x] PR ≤500 LOC.

## Нотатки реалізації

- Одна EF-міграція `20261104210214_MigrateSessionsReportsTargetLanguage` накриває обидві таблиці.
  Для обох: `ADD COLUMN ... NULL` → `UPDATE ... SET 'pl'` → FK через `NOT VALID` + `VALIDATE`
  (без ACCESS EXCLUSIVE на весь час) → `SET NOT NULL`. Інтеримний C#-дефолт `TargetLanguage = "pl"`
  на `LearningSession`/`ContentReport` з якорями `TODO(T13)`/`TODO(T14)` знімається там, де місця
  створення писатимуть мову з контексту. Backfill `'pl'` вибрано тому, що session/report не досяжні
  через `entry` — мову не вивести з даних (на відміну від T08, де backfill брався з `entry_id`).
- **Своп UNIQUE однієї активної session (крок 25→26, критично для AC-04):** глобальний
  `ux_learning_sessions_one_active` (`learner_id WHERE status='active'` — одна активна session на
  learner у всіх мовах) замінено на мово-свідомий `ux_learning_sessions_one_active_lang`
  (`learner_id, target_language WHERE status='active'` — одна активна session НА МОВУ). Своп —
  `CREATE UNIQUE INDEX CONCURRENTLY` нового → `DROP INDEX CONCURRENTLY` старого (обидва
  `suppressTransaction: true`), без вікна без обмеження. EF-конфіг `LearningSessionConfiguration`
  замінює індекс, тож snapshot лишається узгодженим. `.down` відновлює глобальний UNIQUE —
  можливо лише якщо немає >1 активної session на learner у різних мовах (інакше спершу закрити зайві).
- Міграційний тест `SessionsReportsTargetLanguageMigrationTests` (`[DockerFact]`, у CI): мігрує до
  pre-T09 стану, вставляє активну session + open report, мігрує до T09 — перевіряє `varchar(2) NOT NULL`,
  backfill `pl:pl`, присутність мово-свідомого UNIQUE й відсутність глобального, FK; відкат повертає
  `False:False:True:False` (глобальний UNIQUE відновлено, мовний зник, колонок немає).
- `LearningMigrationTests.Sessions_AllowCompletedHistoryButRejectSecondActiveSession` оновлено на нову
  назву індексу `ux_learning_sessions_one_active_lang`. `LearningSchemaAssertions` не чіпали — це
  point-in-time знімок стану `CreateAnswerEvents` (задовго до T09).
