---
id: T02
title: "Заповнити kind для наявних session і зробити колонку NOT NULL (backfill + contract)"
layer: "migration"
deps: ["[T01](./T01-session-kind-expand.md)"]
acs: ["AC-18"]
files_hint: ["docs/features/unlimited-learning/migrations/20261103120001_backfill_kind_in_learning_sessions.up.sql", "docs/features/unlimited-learning/migrations/20261103120002_set_kind_not_null_on_learning_sessions.up.sql", "ConstantLearning/Migrations/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T02 — Заповнити kind для наявних session і зробити колонку NOT NULL (backfill + contract)

**Шар:** `migration` · **Залежить від:** [T01](./T01-session-kind-expand.md) · **AC:** [AC-18](../PRD.md#ac-18---domain-invariant) · **Оцінка:** S

## Why

[data-model §Migrations](../data-model.md): кроки 2 і 3 виконуються окремими розгортаннями після того, як застосунок пише `kind` (T01).

## What

EF-міграція backfill (`plan` для наявних рядків) і окрема EF-міграція SET NOT NULL. Розгортати по черзі, друга — лише після того, як жодна session не має NULL.

Файли: `docs/features/unlimited-learning/migrations/20261103120001_backfill_kind_in_learning_sessions.up.sql`, `docs/features/unlimited-learning/migrations/20261103120002_set_kind_not_null_on_learning_sessions.up.sql`, `ConstantLearning/Migrations/`

## Definition of Done

- [ ] після backfill жодна session не має NULL у `kind` (перевірка в інтеграційному тесті)
- [ ] міграція NOT NULL застосовується й відкочується чисто
- [ ] тести session зелені
- [ ] `dotnet build` і тести без нових попереджень
