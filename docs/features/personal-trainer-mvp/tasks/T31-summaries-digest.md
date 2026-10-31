---
id: T31
title: "Реалізувати щоденний підсумок learner і дайджест редактора"
layer: "app"
deps: ["T29", "T23"]
acs: ["AC-37", "AC-43"]
files_hint: ["ConstantLearning/Features/Notifications/Summaries/", "ConstantLearning.Tests/Notifications/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T31 — Реалізувати щоденний підсумок learner і дайджест редактора

**Шар:** `app` · **Залежить від:** [T29](./T29-notification-dispatcher.md), [T23](./T23-content-report-api.md) · **AC:** AC-37, AC-43 · **Оцінка:** M · **Фаза:** 8

## Why

AC-37, AC-43 у [PRD](../PRD.md); потік «Щоденні підсумки й дайджест» у [sad §6](../sad.md); `summary_dispatch_id` у [data-model.md](../data-model.md).

## What

Підсумок о 12:00 за поясом learner з результатами, закритими після попереднього підсумку; дайджест о 9:00 за Варшавою з reports без рішення; зняття позначки підсумку, якщо відправлення `failed` чи `missed`.

Файли: `ConstantLearning/Features/Notifications/Summaries/`, `ConstantLearning.Tests/Notifications/`

## Definition of Done

- [ ] інтеграційні тести AC-37, AC-43 зелені
- [ ] результат з ненадісланого підсумку потрапляє в наступний і не повторюється після надісланого
- [ ] `dotnet build`, тести й лінтер без нових попереджень
