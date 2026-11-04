---
id: T11
title: "listTargetLanguages + listLearnerLanguages + реєстр помилок"
layer: "app"
deps: ["[T01](./T01-config-target-languages.md)", "[T03](./T03-migrate-identity-day-state.md)", "[T06](./T06-base-course-availability.md)"]
acs: ["AC-01", "AC-10"]
files_hint:
  - "ConstantLearning/Features/Identity/"
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T11 — `listTargetLanguages` + `listLearnerLanguages` + реєстр помилок

**Шар:** app · **Залежить від:** [T01](./T01-config-target-languages.md), [T03](./T03-migrate-identity-day-state.md), [T06](./T06-base-course-availability.md) · **AC:** AC-01, AC-10 · **Оцінка:** M · **Фаза:** 5 (API)

## Why

US-01 потребує списку мов для вибору; екрани потребують переліку мов learner-а й активної.
Реєстр помилок — спільний хаб для решти handler-ів: 4 нові коди фічі мають повертати **той самий
рядок**, що в контракті ([openapi.yaml](../contracts/openapi.yaml),
[api-sync-report §B.2](../contracts/api-sync-report.md), [openapi-rules](../../../../.claude/rules/openapi.md)).

## What

- `GET` `listTargetLanguages` — повний список (без пагінації, ADR-0004) з `available`
  ([api-sync-report §deviations](../contracts/api-sync-report.md)); `ORDER BY sort_order`.
- `GET` `listLearnerLanguages` — мови learner-а з `started_at`/`is_active` + `active_target_language`.
- Завести в реєстрі помилок константи: `language.unknown` (404), `language.not_available` (409),
  `language.not_studying` (409), `language.concurrent_update` (409) — рядок = контракт.
- Відповіді лише власнику-learner (AC-10); `TelegramInitData` (reference ADR-0005).

## Definition of Done

- [ ] Обидві операції віддають форму з `openapi.yaml` (contract-тест зелений на прикладах).
- [ ] `available` обчислюється згідно T06; `pl` available, disabled/без контенту — ні.
- [ ] 4 нові `language.*` коди в реєстрі, кожен відповідає `^[a-z_]+\.[a-z_]+$` і рядку контракту; 1 код = 1 визначення.
- [ ] Тести: 200 happy, 401 `auth.invalid_init_data`, 404 `learner.not_onboarded`; дані лише свого learner (AC-10).
- [ ] PR ≤500 LOC.
