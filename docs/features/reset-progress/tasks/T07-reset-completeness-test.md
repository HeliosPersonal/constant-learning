---
id: T07
title: "Додати тест повноти скидання"
layer: "tests"
deps: ["[T02](./T02-learning-reset-handler.md)", "[T03](./T03-content-reset-handler.md)"]
acs: ["AC-01", "AC-10"]
files_hint: ["ConstantLearning.Tests/Architecture/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T07 — Додати тест повноти скидання

**Шар:** `tests` · **Залежить від:** [T02](./T02-learning-reset-handler.md), [T03](./T03-content-reset-handler.md) · **AC:** AC-01, AC-10 · **Оцінка:** S

## Why

Ризик «новий модуль без обробника» з [SAD §11](../sad.md#11-risks-and-technical-debt).

## What

Архітектурний тест перебирає сутності EF, що посилаються на learner або його слова, і вимагає для кожної обробник скидання **або** запис у явному списку збережених.

Явний whitelist збережених (має бути закодований у тесті, щоб новий learner-FK поза списком провалював перевірку — див. [SAD §8 Completeness](../sad.md#8-crosscutting-concepts)):

- `reminder_schedules` — налаштування нагадувань (кероване Identity/Notifications).
- `notification_dispatches` — черга повідомлень learner (`learner_id`).
- `publication_log.actor_learner_id` — журнал дій content editor.
- `content_reports` — автор-псевдонім лишається; обробник Content лише відвʼязує `answer_event_id` (не видаляє рядок).
- `learners` сам — зберігається; обробник Identity скидає `course_completed_at` і `new_words_available_from` у `NULL`, решту полів профілю/налаштувань/`current_day_*` не чіпає.

Обробник скидання (не whitelist) мають: `learner_words`, `learning_sessions` (усі `kind`), `session_items`, `answer_events` і таблиця нейтралізацій оскаржень.

Файли: `ConstantLearning.Tests/Architecture/`

## Definition of Done

- [ ] тест падає, якщо додати сутність learner без обробника чи запису в список збереженого
- [ ] `dotnet build`, тести й лінтер без нових попереджень
