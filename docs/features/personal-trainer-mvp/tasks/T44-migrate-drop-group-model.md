---
id: T44
title: "Перенести міграцію видалення таблиць групової моделі"
layer: "migration"
deps: ["[T43](T43-retire-group-code.md)", "[T07](T07-migrate-search-jobs-dispatches.md)"]
acs: []
files_hint: ["docs/features/personal-trainer-mvp/migrations/20261031120021_drop_group_model_*", "ConstantLearning/Migrations/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T44 — Перенести міграцію видалення таблиць групової моделі

**Шар:** `migration` · **Залежить від:** [T43](./T43-retire-group-code.md), [T07](./T07-migrate-search-jobs-dispatches.md) · **AC:** — (технічна передумова) · **Оцінка:** S · **Фаза:** 8

## Why

Contract-фаза [ADR-0004](../adr/0004-retire-the-group-model-without-data-migration.md); рішення від 2026-11-01 передбачає спільний реліз вимкнення групового коду й видалення його таблиць.

## What

Перенести `20261031120021_drop_group_model` у живу EF-міграцію після [T43](T43-retire-group-code.md); код і міграцію випустити разом після резервної копії та успішної проби її відновлення в окрему БД.

Файли: `docs/features/personal-trainer-mvp/migrations/20261031120021_drop_group_model_*`, `ConstantLearning/Migrations/`

## Definition of Done

- [ ] міграція застосовується в тому самому релізі, що й видалення групового коду
- [ ] перед релізом створено резервну копію поза кластером і перевірено її відновлення в окрему БД
- [ ] інструкція релізу описує відновлення копії БД у разі невдачі; простий відкат коду не вважається відновленням після видалення таблиць
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

Lane `migration`.
