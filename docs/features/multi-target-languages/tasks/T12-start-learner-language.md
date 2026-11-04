---
id: T12
title: "startLearnerLanguage (ідемпотентний старт + активація)"
layer: "app"
deps: ["[T04](./T04-per-language-day-state.md)", "[T06](./T06-base-course-availability.md)", "[T11](./T11-read-apis-error-registry.md)"]
acs: ["AC-01", "AC-02", "AC-12"]
files_hint:
  - "ConstantLearning/Features/Identity/"
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T12 — `startLearnerLanguage` (ідемпотентний старт + активація)

**Шар:** app · **Залежить від:** [T04](./T04-per-language-day-state.md), [T06](./T06-base-course-availability.md), [T11](./T11-read-apis-error-registry.md) · **AC:** AC-01, AC-02, AC-12 · **Оцінка:** L · **Фаза:** 5 (API)

> **Розблоковано (2026-11-04).** **OQ-4** = **оптимістична конкурентність через наявний `xmin`**,
> ключ `(learner_id, target_language)`: конфлікт версії → `409 language.concurrent_update`, клієнт
> повторює. Без server-side retry й без песимістичних блокувань. [PRD §8 OQ-4](../PRD.md#8-open-questions), [SAD §4](../sad.md).

## Why

US-01/flow 4 ([sad.md §6](../sad.md), [api-sync-report §B.5](../contracts/api-sync-report.md)):
старт мови створює рядок `learner_language_states` і робить мову активною. Мутація **ідемпотентна**
за природним ключем `(learner, target_language)` (AC-12) — без окремого `Idempotency-Key`. Мова без
approved-контенту недоступна (AC-02).

## What

- `POST` `startLearnerLanguage` з `target_language`: INSERT рядка стану + UPDATE
  `learners.active_target_language` з **оптимістичною перевіркою `xmin`** `(learner_id, target_language)`.
- Гілки: 200 happy (AC-01); 200 ідемпотентно, якщо вже вивчає (AC-12, стан не чіпається);
  409 `language.not_available`, якщо недоступна (AC-02, активна мова без змін);
  404 `language.unknown` для невідомого коду; 409 `language.concurrent_update` при `xmin`-конфлікті
  (OQ-4 — клієнт повторює, без server-side retry).
- Старт не чіпає облік інших мов.

## Definition of Done

- [ ] Integration: перший старт створює рядок стану + активує мову + віддає «Сьогодні» з першим заняттям (AC-01).
- [ ] Повторний старт тієї ж мови → 200 без другого рядка, word status/історія без змін (AC-12).
- [ ] Старт мови без approved-контенту → 409 `language.not_available`, активна мова незмінна (AC-02).
- [ ] Конкурентний старт/перемикання: `xmin`-конфлікт → `409 language.concurrent_update` покрито тестом (OQ-4).
- [ ] Contract-тест на `openapi.yaml` (200/404/409) зелений.
- [ ] PR ≤500 LOC.
