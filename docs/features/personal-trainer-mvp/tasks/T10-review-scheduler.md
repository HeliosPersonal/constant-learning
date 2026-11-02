---
id: T10
title: "Реалізувати планувальник повторень за портом над журналом відповідей"
layer: "domain"
deps: ["[T01](T01-module-skeleton.md)"]
acs: ["AC-08", "AC-18", "AC-19", "AC-22", "AC-23"]
files_hint: ["ConstantLearning/Features/Learning/Domain/Scheduling/", "ConstantLearning.Tests/Learning/Domain/Scheduling/"]
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T10 — Реалізувати планувальник повторень за портом над журналом відповідей

**Шар:** `domain` · **Залежить від:** [T01](./T01-module-skeleton.md) · **AC:** AC-08, AC-18, AC-19, AC-22, AC-23 · **Оцінка:** L · **Фаза:** 2

## Why

Правила «Інтервали» й «Пріоритет повторень» з [PRD §5](../PRD.md#5-acceptance-criteria); [ADR-0009](../adr/0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md).

## What

Порт `IReviewScheduler` і реалізація MEMORIZE з обмеженням інтервалу межами PRD; частка прострочення для пріоритету; переходи word status «На повторення» ↔ «Вже пригадую» і control repetition.

Файли: `ConstantLearning/Features/Learning/Domain/Scheduling/`, `ConstantLearning.Tests/Learning/Domain/Scheduling/`

## Definition of Done

- [ ] property-тест: інтервал після правильної відповіді завжди в межах `[min(180, max(2, prev+1)); min(3·prev, 180)]`; за фактичного `prev >= 180` — рівно 180 діб від відповіді, зокрема після тривалої перерви й на послідовних повтореннях
- [ ] unit-тести AC-18, AC-19, AC-22, AC-23 зелені
- [ ] пропущені доби не створюють mistake (AC-08)
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

Параметри MEMORIZE — стартові значення, OQ-4.
