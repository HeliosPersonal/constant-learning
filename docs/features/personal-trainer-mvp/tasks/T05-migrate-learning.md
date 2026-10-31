---
id: T05
title: "Перенести міграції слів learner, session, карток і журналу відповідей"
layer: "migration"
deps: ["T04"]
acs: []
files_hint: ["docs/features/personal-trainer-mvp/migrations/20261031120007_create_learner_words_*", "docs/features/personal-trainer-mvp/migrations/20261031120008_create_learning_sessions_*", "docs/features/personal-trainer-mvp/migrations/20261031120009_create_session_items_*", "docs/features/personal-trainer-mvp/migrations/20261031120010_create_answer_events_*", "ConstantLearning/Migrations/", "ConstantLearning/Features/Learning/Data/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T05 — Перенести міграції слів learner, session, карток і журналу відповідей

**Шар:** `migration` · **Залежить від:** [T04](./T04-migrate-content-core.md) · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 4

## Why

Схема з [data-model.md](../data-model.md); staged-пари в [migrations/](../migrations/). Журнал відповідей — [ADR-0009](../adr/0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md).

## What

Перенести staged-пари `20261031120007_create_learner_words`, `20261031120008_create_learning_sessions`, `20261031120009_create_session_items`, `20261031120010_create_answer_events` у живі EF-міграції через `migrationBuilder.Sql` з новою позначкою часу, зберігши порядок; описати сутності й EF-конфігурації так, щоб snapshot збігався з DDL.

Файли: `docs/features/personal-trainer-mvp/migrations/20261031120007_create_learner_words_*`, `docs/features/personal-trainer-mvp/migrations/20261031120008_create_learning_sessions_*`, `docs/features/personal-trainer-mvp/migrations/20261031120009_create_session_items_*`, `docs/features/personal-trainer-mvp/migrations/20261031120010_create_answer_events_*`, `ConstantLearning/Migrations/`, `ConstantLearning/Features/Learning/Data/`

## Definition of Done

- [ ] міграції застосовуються на чистій БД і відкочуються `.down.sql` без залишків
- [ ] snapshot EF не породжує нової міграції після `dotnet ef migrations add Check`
- [ ] самоперевірка `.claude/rules/migrations.md` без порушень
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

Lane `migration` — виконується послідовно з іншими міграціями.
