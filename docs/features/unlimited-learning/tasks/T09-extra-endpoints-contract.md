---
id: T09
title: "Додати дії extra в API, зареєструвати коди помилок і внести зміни в контракт батьківської фічі"
layer: "ports"
deps: ["[T05](./T05-extra-availability-today.md)", "[T06](./T06-start-extra-word.md)", "[T07](./T07-start-extra-practice.md)"]
acs: ["AC-04", "AC-10", "AC-17", "AC-19"]
files_hint: ["ConstantLearning/Features/Learning/Sessions/Extra/", "ConstantLearning/Features/Learning/", "docs/features/personal-trainer-mvp/contracts/openapi.yaml", "docs/features/unlimited-learning/contracts/openapi.yaml"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T09 — Додати дії extra в API, зареєструвати коди помилок і внести зміни в контракт батьківської фічі

**Шар:** `ports` · **Залежить від:** [T05](./T05-extra-availability-today.md), [T06](./T06-start-extra-word.md), [T07](./T07-start-extra-practice.md) · **AC:** [AC-04](../PRD.md#ac-04---error), [AC-10](../PRD.md#ac-10---error), [AC-17](../PRD.md#ac-17---domain-invariant), [AC-19](../PRD.md#ac-19---authorization) · **Оцінка:** S

## Why

[openapi](../contracts/openapi.yaml), [api-sync-report](../contracts/api-sync-report.md): Deferrals 1 — реєстр помилок і зміни наявних схем.

## What

Контролери для startExtraWord і startExtraPractice за контрактом; додати коди extra.plan_not_done, extra.course_completed, extra.no_eligible_words, extra.rate_limited в реєстр помилок Learning з тими самими рядками, що в контракті. Внести в контракт personal-trainer-mvp зміни з x-amendments (extra_actions, Session.kind, нові значення SessionItem.kind) і перезапустити синхронізацію контракту.

Файли: `ConstantLearning/Features/Learning/Sessions/Extra/`, `ConstantLearning/Features/Learning/`, `docs/features/personal-trainer-mvp/contracts/openapi.yaml`, `docs/features/unlimited-learning/contracts/openapi.yaml`

## Definition of Done

- [ ] інтеграційні тести авторизації AC-19 і відповідей-помилок зелені (чужі дані не розкриваються)
- [ ] кожен код помилки з контракту має одне визначення в реєстрі з тим самим рядком
- [ ] api-sync-report після повторного запуску має Verdict PASS або PASS (with deferrals)
- [ ] `dotnet build` і тести без нових попереджень
