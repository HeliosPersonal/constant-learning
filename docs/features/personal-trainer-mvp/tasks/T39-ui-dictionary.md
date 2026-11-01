---
id: T39
title: "Зробити пошук у словнику й картку запису"
layer: "ui"
deps: ["[T34](T34-miniapp-skeleton.md)", "[T22](T22-dictionary-search-api.md)"]
acs: ["AC-30", "AC-31"]
files_hint: ["ConstantLearning.MiniApp/src/features/dictionary/search/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T39 — Зробити пошук у словнику й картку запису

**Шар:** `ui` · **Залежить від:** [T34](./T34-miniapp-skeleton.md), [T22](./T22-dictionary-search-api.md) · **AC:** AC-30, AC-31 · **Оцінка:** M · **Фаза:** 9

## Why

US-08 у [PRD](../PRD.md); [ADR-0012](../adr/0012-search-via-a-precomputed-table-of-folded-word-forms.md).

## What

Запит на кожен символ з відміною застарілого, результати й картка запису, порожній стан з дією «повідомити про відсутній запис»; lazy-чанк.

Файли: `ConstantLearning.MiniApp/src/features/dictionary/search/`

## Definition of Done

- [ ] екран і його стани використовують токени [Everforest](../design-system.md) та коректно відображаються у світлій і темній темах

- [ ] компонентні тести AC-30, AC-31 зелені
- [ ] застарілий запит скасовується
- [ ] `npm run lint`, `npm run typecheck` і `npm test` зелені
