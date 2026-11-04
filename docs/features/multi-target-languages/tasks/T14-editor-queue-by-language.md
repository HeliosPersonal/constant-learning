---
id: T14
title: "listEditorQueueByLanguage + прив'язка report до мови запису"
layer: "app"
deps: ["[T09](./T09-migrate-sessions-reports.md)", "[T11](./T11-read-apis-error-registry.md)"]
acs: ["AC-09", "AC-11"]
files_hint:
  - "ConstantLearning/Features/Content/"
owner: "<TBD Security Lead>"
estimate: "M"
status: "blocked"
---

# T14 — `listEditorQueueByLanguage` + прив'язка report до мови запису

**Шар:** app · **Залежить від:** [T09](./T09-migrate-sessions-reports.md), [T11](./T11-read-apis-error-registry.md) · **AC:** AC-09, AC-11 · **Оцінка:** M · **Фаза:** 5 (API)

> **Blocked — OQ-9** (owner: Security Lead): чи обмежувати редактора мовами, які він здатен
> верифікувати. Поки роль content editor єдина для всіх мов — операція без per-language authz; рішення
> «обмежити» додасть 403-гілку. [PRD §6.1](../PRD.md#61-security--privacy), [api-sync-report §open items](../contracts/api-sync-report.md).

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

- [ ] Черга фільтрується за `target_language`; повідомлення різних мов не змішуються (AC-11); курсорна пагінація за контрактом.
- [ ] Новий report отримує `target_language` запису з `entry_id`; не змінює контент інших мов (AC-09).
- [ ] Схвалення/відкат зачіпає лише словник мови повідомлення (integration-тест).
- [ ] OQ-9 закрито Security Lead (єдина роль → без 403; «обмежити» → додано 403-гілку + тест).
- [ ] Contract-тест на `openapi.yaml` зелений.
- [ ] PR ≤500 LOC.
