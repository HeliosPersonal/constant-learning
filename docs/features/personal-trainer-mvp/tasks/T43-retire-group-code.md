---
id: T43
title: "Видалити код групової моделі"
layer: "wiring"
deps: ["[T32](T32-bot-webhook.md)", "[T07](T07-migrate-search-jobs-dispatches.md)", "[T20](T20-dictionary-import.md)", "[T34](T34-miniapp-skeleton.md)"]
acs: []
files_hint: ["ConstantLearning/Services/", "ConstantLearning/Jobs/", "ConstantLearning/Data/", "ConstantLearning/Enums/", "ConstantLearning/Resources/", "ConstantLearning/Program.cs", "ConstantLearning.Tests/Services/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "done"
---

# T43 — Видалити код групової моделі

**Шар:** `wiring` · **Залежить від:** [T32](./T32-bot-webhook.md), [T07](./T07-migrate-search-jobs-dispatches.md), [T20](./T20-dictionary-import.md), [T34](./T34-miniapp-skeleton.md) · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 7

## Why

[ADR-0004](../adr/0004-retire-the-group-model-without-data-migration.md).

## What

Прибрати групові сервіси, Quartz-завдання й команди, зокрема `/startlearning`, `/stoplearning`, `/restartprogress`, налаштування розкладу та `/getalllearned`. Зберегти `/start` нового модуля `Bot`, який відкриває Mini App. Ця зміна видаляє код, DI та тексти групового бота; сутності й таблиці `AppDbContext` лишаються до окремої міграції за [T44](T44-migrate-drop-group-model.md).

Файли: `ConstantLearning/Services/`, `ConstantLearning/Jobs/`, `ConstantLearning/Data/`, `ConstantLearning/Enums/`, `ConstantLearning/Resources/`, `ConstantLearning/Program.cs`, `ConstantLearning.Tests/Services/`

## Definition of Done

- [x] `dotnet build` без групового коду
- [ ] `dotnet test` зелені без групового коду
- [x] групові команди, сервіси, Quartz-завдання та реєстрація в DI видалені; `/start` Mini App збережено
- [ ] сутності й таблиці буде видалено окремо міграцією [T44](T44-migrate-drop-group-model.md)
