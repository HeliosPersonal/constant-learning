---
id: T07
title: "Перенести міграції форм пошуку, довідок, задач звірки й відправлень"
layer: "migration"
deps: ["T06"]
acs: []
files_hint: ["docs/features/personal-trainer-mvp/migrations/20261031120016_create_search_forms_*", "docs/features/personal-trainer-mvp/migrations/20261031120017_create_morph_forms_*", "docs/features/personal-trainer-mvp/migrations/20261031120018_create_reference_entries_*", "docs/features/personal-trainer-mvp/migrations/20261031120019_create_verification_jobs_*", "docs/features/personal-trainer-mvp/migrations/20261031120020_create_notification_dispatches_*", "docs/features/personal-trainer-mvp/migrations/20261031120022_alter_content_reports_dedup_and_summary_*", "ConstantLearning/Migrations/", "ConstantLearning/Features/Content/Data/", "ConstantLearning/Features/Notifications/Data/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T07 — Перенести міграції форм пошуку, довідок, задач звірки й відправлень

**Шар:** `migration` · **Залежить від:** [T06](./T06-migrate-reports-publication.md) · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 6

## Why

Схема з [data-model.md](../data-model.md); staged-пари в [migrations/](../migrations/). Пара `0022` іде одразу після `0020`, бо посилається на `notification_dispatches`; `0021` — окремий реліз (T44).

## What

Перенести staged-пари `20261031120016_create_search_forms`, `20261031120017_create_morph_forms`, `20261031120018_create_reference_entries`, `20261031120019_create_verification_jobs`, `20261031120020_create_notification_dispatches`, `20261031120022_alter_content_reports_dedup_and_summary` у живі EF-міграції через `migrationBuilder.Sql` з новою позначкою часу, зберігши порядок; описати сутності й EF-конфігурації так, щоб snapshot збігався з DDL.

Файли: `docs/features/personal-trainer-mvp/migrations/20261031120016_create_search_forms_*`, `docs/features/personal-trainer-mvp/migrations/20261031120017_create_morph_forms_*`, `docs/features/personal-trainer-mvp/migrations/20261031120018_create_reference_entries_*`, `docs/features/personal-trainer-mvp/migrations/20261031120019_create_verification_jobs_*`, `docs/features/personal-trainer-mvp/migrations/20261031120020_create_notification_dispatches_*`, `docs/features/personal-trainer-mvp/migrations/20261031120022_alter_content_reports_dedup_and_summary_*`, `ConstantLearning/Migrations/`, `ConstantLearning/Features/Content/Data/`, `ConstantLearning/Features/Notifications/Data/`

## Definition of Done

- [ ] міграції застосовуються на чистій БД і відкочуються `.down.sql` без залишків
- [ ] snapshot EF не породжує нової міграції після `dotnet ef migrations add Check`
- [ ] самоперевірка `.claude/rules/migrations.md` без порушень
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

Lane `migration` — виконується послідовно з іншими міграціями.
