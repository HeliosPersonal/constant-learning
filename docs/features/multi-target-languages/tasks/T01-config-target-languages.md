---
id: T01
title: "Конфіг-таблиця target_languages + seed + сутність/репозиторій"
layer: "migration+infra"
deps: []
acs: ["AC-01", "AC-02"]
files_hint:
  - "ConstantLearning/Features/Identity/Data/"
  - "ConstantLearning/Migrations/"
  - "ConstantLearning.Tests/Fixtures/TargetLanguageFixtures.cs"
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T01 — Конфіг-таблиця `target_languages` + seed + сутність/репозиторій

**Шар:** migration+infra · **Залежить від:** — · **AC:** AC-01, AC-02 · **Оцінка:** M · **Фаза:** 1 (Фундамент)

## Why

Набір підтримуваних мов — дані/конфіг, а не enum у коді
([ADR-0004](../adr/0004-model-supported-languages-as-config-table.md), [SAD §4 стовп 4](../sad.md)):
нова мова = рядок конфігу + контент без зміни коду тренера. Усі таблиці прогресу й контенту
посилатимуться на `target_languages.code` як на природний ключ-дискримінатор
([data-model.md §New entities](../data-model.md)). Це фундамент решти фічі.

## What

- Стейджені пари [`20261104090000_create_target_languages`](../migrations) і
  [`20261104090100_seed_target_languages`](../migrations) перенести в EF Core міграцію
  (`migrationBuilder.Sql`, нова позначка часу; порядок — як у [data-model.md §Міграційний план](../data-model.md) кроки 1–2).
- Сутність `TargetLanguage` в `Identity/Data/` (`code` PK VARCHAR(2), `display_name_uk`,
  `is_enabled`, `sort_order`, `normalization_profile`, `created_at`) + EF-конфіг.
- Репозиторій/читач каталогу: список мов `ORDER BY sort_order`.
- Seed `pl` (`is_enabled=true`, `normalization_profile='pl'`), `en`/`de`/`nl`
  (`is_enabled=false`, `normalization_profile='en'`/`'de'`/`'nl'` — профілі заведено одразу,
  правила кодуються в T07a/T07b після spike OQ-7 ([T07s](./T07s-spike-language-sources.md)); OQ-5 закрито) — `ON CONFLICT (code) DO NOTHING`.
- Фабрика `TargetLanguageFixtures.NewEnabledLanguage(code, normalizationProfile)`
  у `ConstantLearning.Tests/Fixtures/` ([data-model.md §Test fixtures](../data-model.md)).

## Definition of Done

- [ ] EF-міграція створює `target_languages` (`CREATE TABLE IF NOT EXISTS`) і виконує seed (`ON CONFLICT DO NOTHING`); застосовано на PostgreSQL 16 testcontainer.
- [ ] `.down` реверсує (DROP таблиці; seed-рядки знято DELETE); self-check міграцій зелений (naming, down, без `CHECK`/`TRIGGER`/бізнес-`DEFAULT`).
- [ ] Репозиторій повертає 4 мови в порядку `sort_order`; `pl` enabled, решта disabled.
- [ ] `TargetLanguageFixtures.NewEnabledLanguage` використовується мінімум в одному тесті каталогу.
- [ ] PR ≤500 LOC.
