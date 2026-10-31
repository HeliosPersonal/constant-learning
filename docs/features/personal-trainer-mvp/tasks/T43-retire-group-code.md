---
id: T43
title: "Видалити код групової моделі"
layer: "wiring"
deps: ["T32", "T07", "T20", "T34"]
acs: []
files_hint: ["ConstantLearning/Services/", "ConstantLearning/Jobs/", "ConstantLearning/Data/", "ConstantLearning/Enums/", "ConstantLearning/Resources/", "ConstantLearning/Program.cs", "ConstantLearning.Tests/Services/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T43 — Видалити код групової моделі

**Шар:** `wiring` · **Залежить від:** [T32](./T32-bot-webhook.md), [T07](./T07-migrate-search-jobs-dispatches.md), [T20](./T20-dictionary-import.md), [T34](./T34-miniapp-skeleton.md) · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 7

## Why

[ADR-0004](../adr/0004-retire-the-group-model-without-data-migration.md).

## What

Прибрати групові сервіси, Quartz-завдання, команди, сутності з `AppDbContext` і тексти бота; залишити лише те, що використовує новий модуль `Bot`. Snapshot EF після цього очікує видалення таблиць — його фіксує T44.

Файли: `ConstantLearning/Services/`, `ConstantLearning/Jobs/`, `ConstantLearning/Data/`, `ConstantLearning/Enums/`, `ConstantLearning/Resources/`, `ConstantLearning/Program.cs`, `ConstantLearning.Tests/Services/`

## Definition of Done

- [ ] `dotnet build` і тести зелені без групового коду
- [ ] у коді немає посилань на `ChatRegistration`, `LearnedWord`, `NewWordsJob`
- [ ] `dotnet build`, тести й лінтер без нових попереджень
