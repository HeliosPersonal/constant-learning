---
id: T03
title: "Перенести міграції learner і нагадувань"
layer: "migration"
deps: ["[T01](T01-module-skeleton.md)"]
acs: []
files_hint: ["docs/features/personal-trainer-mvp/migrations/20261031120000_create_learners_*", "docs/features/personal-trainer-mvp/migrations/20261031120001_create_reminder_schedules_*", "ConstantLearning/Migrations/", "ConstantLearning/Features/Identity/Data/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T03 — Перенести міграції learner і нагадувань

**Шар:** `migration` · **Залежить від:** [T01](./T01-module-skeleton.md) · **AC:** — (технічна передумова) · **Оцінка:** S · **Фаза:** 2

## Why

Схема з [data-model.md](../data-model.md); staged-пари в [migrations/](../migrations/).

## What

Перенести staged-пари `20261031120000_create_learners`, `20261031120001_create_reminder_schedules` у живі EF-міграції через `migrationBuilder.Sql` з новою позначкою часу, зберігши порядок; описати сутності й EF-конфігурації так, щоб snapshot збігався з DDL.

Файли: `docs/features/personal-trainer-mvp/migrations/20261031120000_create_learners_*`, `docs/features/personal-trainer-mvp/migrations/20261031120001_create_reminder_schedules_*`, `ConstantLearning/Migrations/`, `ConstantLearning/Features/Identity/Data/`

## Definition of Done

- [ ] міграції застосовуються на чистій БД і відкочуються `.down.sql` без залишків
- [ ] snapshot EF не породжує нової міграції після `dotnet ef migrations add Check`
- [ ] самоперевірка `.claude/rules/migrations.md` без порушень
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

Lane `migration` — виконується послідовно з іншими міграціями.
