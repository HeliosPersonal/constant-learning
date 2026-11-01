---
id: T25
title: "Завантажити довідкові знімки для звірки"
layer: "infra"
deps: ["[T07](T07-migrate-search-jobs-dispatches.md)"]
acs: ["AC-35"]
files_hint: ["ConstantLearning/Features/Content/References/", "ConstantLearning.Tests/Content/References/"]
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T25 — Завантажити довідкові знімки для звірки

**Шар:** `infra` · **Залежить від:** [T07](./T07-migrate-search-jobs-dispatches.md) · **AC:** AC-35 · **Оцінка:** L · **Фаза:** 7

## Why

[ADR-0011](../adr/0011-verify-content-reports-asynchronously-against-reference-snapshots.md); ризики Wikisłownik і WSJP у [sad §11](../sad.md#11-risks-and-technical-debt).

## What

Порт `IReferenceSource`, адаптер дампу Wikisłownik у `reference_entries` з атрибуцією CC BY-SA, конвертер IPA → кирилиця; WSJP — лише точкові запити з обмеженням частоти.

Файли: `ConstantLearning/Features/Content/References/`, `ConstantLearning.Tests/Content/References/`

## Definition of Done

- [ ] тести адаптера на зафіксованих зразках вікітексту зелені
- [ ] збій парсера переводить запис у ручний режим, а не падає
- [ ] `dotnet build`, тести й лінтер без нових попереджень
