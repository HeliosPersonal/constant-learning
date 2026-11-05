---
id: T14
title: "listEditorQueueByLanguage + прив'язка report до мови запису"
layer: "app"
deps: ["[T09](./T09-migrate-sessions-reports.md)", "[T11](./T11-read-apis-error-registry.md)"]
acs: ["AC-09", "AC-11"]
files_hint:
  - "ConstantLearning/Features/Content/"
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "done"
---

# T14 — `listEditorQueueByLanguage` + прив'язка report до мови запису

**Шар:** app · **Залежить від:** [T09](./T09-migrate-sessions-reports.md), [T11](./T11-read-apis-error-registry.md) · **AC:** AC-09, AC-11 · **Оцінка:** M · **Фаза:** 5 (API)

> **Розблоковано (2026-11-04).** **OQ-9** = роль content editor **лишається єдиною для всіх мов** —
> операція **без per-language authz й без 403-гілки**. AC-11 — коректність маршрутизації (повідомлення й
> схвалення/відкат лягають на словник правильної мови), не контроль доступу. Ревізія — коли редакторів
> і мов побільшає. [PRD §6.1](../PRD.md#61-security--privacy), [api-sync-report §open items](../contracts/api-sync-report.md).

## Why

AC-09: content report прив'язується до запису **активної мови** learner-а (сервер виводить
`target_language` з `entry_id`, тіло не змінюється); AC-11: черга редактора відокремлює повідомлення
за мовою, схвалення/відкат лягає лише на словник тієї мови (flow 6/7, [sad.md §6](../sad.md)).

## What

- `GET` `listEditorQueueByLanguage` з `target_language`: `content_reports WHERE target_language=?`
  `ORDER BY created_at`, **курсорна пагінація** (індекс `ix_content_reports_lang_status_created`).
- Створення report: сервер виводить `target_language` з `entry_id` (денормалізація T09), тіло запиту без змін (AC-09).
- Схвалення/відкат застосовується лише до словника мови повідомлення (AC-11).
- Добовий ліміт повідомлень per-language не збільшує сукупний ліміт на learner ([PRD §6.1](../PRD.md#61-security--privacy)).

## Definition of Done

- [x] Черга фільтрується за `target_language`; повідомлення різних мов не змішуються (AC-11); курсорна пагінація за контрактом.
- [x] Новий report отримує `target_language` запису з `entry_id`; не змінює контент інших мов (AC-09).
- [x] Схвалення/відкат зачіпає лише словник мови повідомлення (integration-тест).
- [x] Жодної per-language authz-гілки (OQ-9 = єдина роль); маршрутизація зміни за мовою покрита integration-тестом.
- [x] Contract-тест на `openapi.yaml` зелений.
- [x] PR ≤500 LOC.
