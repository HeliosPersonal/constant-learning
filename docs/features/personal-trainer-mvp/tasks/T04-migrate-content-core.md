---
id: T04
title: "Перенести міграції записів, версій, перекладів і прийнятих відповідей"
layer: "migration"
deps: ["[T03](T03-migrate-identity.md)"]
acs: []
files_hint: ["docs/features/personal-trainer-mvp/migrations/20261031120002_create_dictionary_entries_*", "docs/features/personal-trainer-mvp/migrations/20261031120003_create_entry_versions_*", "docs/features/personal-trainer-mvp/migrations/20261031120004_add_published_version_fk_to_dictionary_entries_*", "docs/features/personal-trainer-mvp/migrations/20261031120005_create_version_translations_*", "docs/features/personal-trainer-mvp/migrations/20261031120006_create_version_accepted_answers_*", "ConstantLearning/Migrations/", "ConstantLearning/Features/Content/Data/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T04 — Перенести міграції записів, версій, перекладів і прийнятих відповідей

**Шар:** `migration` · **Залежить від:** [T03](./T03-migrate-identity.md) · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 3

## Why

Схема з [data-model.md](../data-model.md); staged-пари в [migrations/](../migrations/). Версійність — [ADR-0010](../adr/0010-version-dictionary-entries-in-postgres-with-a-published-pointer.md).

## What

Перенести staged-пари `20261031120002_create_dictionary_entries`, `20261031120003_create_entry_versions`, `20261031120004_add_published_version_fk_to_dictionary_entries`, `20261031120005_create_version_translations`, `20261031120006_create_version_accepted_answers` у живі EF-міграції через `migrationBuilder.Sql` з новою позначкою часу, зберігши порядок; описати сутності й EF-конфігурації так, щоб snapshot збігався з DDL.

Файли: `docs/features/personal-trainer-mvp/migrations/20261031120002_create_dictionary_entries_*`, `docs/features/personal-trainer-mvp/migrations/20261031120003_create_entry_versions_*`, `docs/features/personal-trainer-mvp/migrations/20261031120004_add_published_version_fk_to_dictionary_entries_*`, `docs/features/personal-trainer-mvp/migrations/20261031120005_create_version_translations_*`, `docs/features/personal-trainer-mvp/migrations/20261031120006_create_version_accepted_answers_*`, `ConstantLearning/Migrations/`, `ConstantLearning/Features/Content/Data/`

## Definition of Done

- [ ] міграції застосовуються на чистій БД і відкочуються `.down.sql` без залишків
- [ ] snapshot EF не породжує нової міграції після `dotnet ef migrations add Check`
- [ ] самоперевірка `.claude/rules/migrations.md` без порушень
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

Lane `migration` — виконується послідовно з іншими міграціями.
