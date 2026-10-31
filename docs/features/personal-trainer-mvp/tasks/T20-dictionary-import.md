---
id: T20
title: "Імпортувати редакторський CSV у версійований словник"
layer: "infra"
deps: ["T04", "T06"]
acs: ["AC-32", "AC-33"]
files_hint: ["ConstantLearning/Features/Content/Import/", "ConstantLearning/Features/Content/Publishing/", "ConstantLearning/HostedServices/DatabaseInitializationService.cs", "docs/dictionary/content-schema.md", "ConstantLearning.Tests/Content/Import/"]
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T20 — Імпортувати редакторський CSV у версійований словник

**Шар:** `infra` · **Залежить від:** [T04](./T04-migrate-content-core.md), [T06](./T06-migrate-reports-publication.md) · **AC:** AC-32, AC-33 · **Оцінка:** L · **Фаза:** 6

## Why

[ADR-0010](../adr/0010-version-dictionary-entries-in-postgres-with-a-published-pointer.md); PRD §1 про робочу копію словника; [content-schema.md](../../../dictionary/content-schema.md).

## What

Розбір CSV з лапками, нова версія лише за зміни `content_hash`, публікація лише `approved`, зняття публікації при втраті `approved`, запис у `publication_log`; спільний сервіс публікації версії для імпорту, звірки, редактора й відкату. Перемкнути `DatabaseInitializationService` з `WordImportService` на новий імпорт. Додати поле прийнятих польських варіантів у `content-schema.md` і шаблон CSV.

Файли: `ConstantLearning/Features/Content/Import/`, `ConstantLearning/Features/Content/Publishing/`, `ConstantLearning/HostedServices/DatabaseInitializationService.cs`, `docs/dictionary/content-schema.md`, `ConstantLearning.Tests/Content/Import/`

## Definition of Done

- [ ] інтеграційний тест: повторний імпорт без змін не створює версій
- [ ] AC-32, AC-33: запис без `approved` не має `published_version_id`, а в `publication_log` є рядок `unpublish`; версії й історія learner лишаються
- [ ] старий `WordImportService` не викликається для нової моделі
- [ ] `dotnet build`, тести й лінтер без нових попереджень
