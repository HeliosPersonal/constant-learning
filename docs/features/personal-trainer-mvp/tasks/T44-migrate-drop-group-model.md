---
id: T44
title: "Перенести міграцію видалення таблиць групової моделі"
layer: "migration"
deps: ["T43", "T07"]
acs: []
files_hint: ["docs/features/personal-trainer-mvp/migrations/20261031120021_drop_group_model_*", "ConstantLearning/Migrations/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T44 — Перенести міграцію видалення таблиць групової моделі

**Шар:** `migration` · **Залежить від:** [T43](./T43-retire-group-code.md), [T07](./T07-migrate-search-jobs-dispatches.md) · **AC:** — (технічна передумова) · **Оцінка:** S · **Фаза:** 8

## Why

Contract-фаза [ADR-0004](../adr/0004-retire-the-group-model-without-data-migration.md); примітка про окремий реліз в [аудиті](../_audit/data-model-2026-10-31.md).

## What

Перенести `20261031120021_drop_group_model` у живу EF-міграцію після T43; застосовувати окремим релізом після `pg_dump`.

Файли: `docs/features/personal-trainer-mvp/migrations/20261031120021_drop_group_model_*`, `ConstantLearning/Migrations/`

## Definition of Done

- [ ] міграція застосовується після видалення групового коду й відкочується до структури без даних
- [ ] у PR є інструкція про `pg_dump` перед релізом
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

Lane `migration`.
