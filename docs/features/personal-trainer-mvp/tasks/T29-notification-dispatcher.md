---
id: T29
title: "Реалізувати dispatcher таблиці відправлень"
layer: "infra"
deps: ["T07", "T02", "T32"]
acs: []
files_hint: ["ConstantLearning/Features/Notifications/Dispatch/", "ConstantLearning.Tests/Notifications/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T29 — Реалізувати dispatcher таблиці відправлень

**Шар:** `infra` · **Залежить від:** [T07](./T07-migrate-search-jobs-dispatches.md), [T02](./T02-test-harness.md), [T32](./T32-bot-webhook.md) · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 7

## Why

[ADR-0007](../adr/0007-dispatch-notifications-through-postgres-dispatch-table.md); канал `notification_dispatches` в [events.md](../contracts/events.md).

## What

Фоновий тик кожні 30 с: захоплення належних рядків з пропуском заблокованих, до 3 спроб, `missed` після 60 хв простою, `failed` після вичерпання; заблокований бот вимикає `learners.can_message`. Quartz-завдання групової моделі не використовуються.

Файли: `ConstantLearning/Features/Notifications/Dispatch/`, `ConstantLearning.Tests/Notifications/`

## Definition of Done

- [ ] інтеграційний тест: два паралельні тики не надсилають один рядок двічі
- [ ] прострочений понад 60 хв рядок стає `missed`
- [ ] три тимчасові помилки Telegram дають `failed`
- [ ] відмова «бот заблоковано» вимикає `learners.can_message`
- [ ] `dotnet build`, тести й лінтер без нових попереджень
