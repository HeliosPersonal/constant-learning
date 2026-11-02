---
id: T26
title: "Реалізувати фонову звірку content reports"
layer: "app"
deps: ["[T23](T23-content-report-api.md)", "[T25](T25-reference-snapshots.md)", "[T20](T20-dictionary-import.md)", "[T21](T21-search-forms.md)"]
acs: ["AC-35", "AC-42"]
files_hint: ["ConstantLearning/Features/Content/Verification/", "ConstantLearning.Tests/Content/Verification/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T26 — Реалізувати фонову звірку content reports

**Шар:** `app` · **Залежить від:** [T23](./T23-content-report-api.md), [T25](./T25-reference-snapshots.md), [T20](./T20-dictionary-import.md), [T21](./T21-search-forms.md) · **AC:** AC-35, AC-42 · **Оцінка:** M · **Фаза:** 8

## Why

Потік звірки в [sad §6](../sad.md); `content_report.verification_requested.v1` в [events.md](../contracts/events.md).

## What

Тик захоплює `verification_jobs` з пропуском заблокованих рядків, до 3 спроб з експоненційною затримкою, dead-letter у чергу редактора; підтверджене значення — нова версія через спільний сервіс публікації, доказ у `validation_evidence`, закриття report.

Файли: `ConstantLearning/Features/Content/Verification/`, `ConstantLearning.Tests/Content/Verification/`

## Definition of Done

- [ ] інтеграційні тести: підтверджено → нова опублікована версія (AC-35, AC-42); недоступне джерело тричі → черга редактора
- [ ] повторне захоплення обробленої задачі нічого не змінює
- [ ] `dotnet build`, тести й лінтер без нових попереджень
