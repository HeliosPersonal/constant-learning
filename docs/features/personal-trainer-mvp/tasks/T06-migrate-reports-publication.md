---
id: T06
title: "Перенести міграції content reports, нейтралізацій, доказів і журналу публікацій"
layer: "migration"
deps: ["T05"]
acs: []
files_hint: ["docs/features/personal-trainer-mvp/migrations/20261031120011_create_content_reports_*", "docs/features/personal-trainer-mvp/migrations/20261031120012_add_content_report_fk_to_entry_versions_*", "docs/features/personal-trainer-mvp/migrations/20261031120013_create_answer_neutralizations_*", "docs/features/personal-trainer-mvp/migrations/20261031120014_create_validation_evidence_*", "docs/features/personal-trainer-mvp/migrations/20261031120015_create_publication_log_*", "ConstantLearning/Migrations/", "ConstantLearning/Features/Content/Data/", "ConstantLearning/Features/Learning/Data/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T06 — Перенести міграції content reports, нейтралізацій, доказів і журналу публікацій

**Шар:** `migration` · **Залежить від:** [T05](./T05-migrate-learning.md) · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 5

## Why

Схема з [data-model.md](../data-model.md); staged-пари в [migrations/](../migrations/).

## What

Перенести staged-пари `20261031120011_create_content_reports`, `20261031120012_add_content_report_fk_to_entry_versions`, `20261031120013_create_answer_neutralizations`, `20261031120014_create_validation_evidence`, `20261031120015_create_publication_log` у живі EF-міграції через `migrationBuilder.Sql` з новою позначкою часу, зберігши порядок; описати сутності й EF-конфігурації так, щоб snapshot збігався з DDL.

Файли: `docs/features/personal-trainer-mvp/migrations/20261031120011_create_content_reports_*`, `docs/features/personal-trainer-mvp/migrations/20261031120012_add_content_report_fk_to_entry_versions_*`, `docs/features/personal-trainer-mvp/migrations/20261031120013_create_answer_neutralizations_*`, `docs/features/personal-trainer-mvp/migrations/20261031120014_create_validation_evidence_*`, `docs/features/personal-trainer-mvp/migrations/20261031120015_create_publication_log_*`, `ConstantLearning/Migrations/`, `ConstantLearning/Features/Content/Data/`, `ConstantLearning/Features/Learning/Data/`

## Definition of Done

- [ ] міграції застосовуються на чистій БД і відкочуються `.down.sql` без залишків
- [ ] snapshot EF не породжує нової міграції після `dotnet ef migrations add Check`
- [ ] самоперевірка `.claude/rules/migrations.md` без порушень
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

Lane `migration` — виконується послідовно з іншими міграціями.
