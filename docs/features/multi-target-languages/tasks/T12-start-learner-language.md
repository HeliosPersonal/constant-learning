---
id: T12
title: "startLearnerLanguage (ідемпотентний старт + активація)"
layer: "app"
deps: ["[T04](./T04-per-language-day-state.md)", "[T06](./T06-base-course-availability.md)", "[T11](./T11-read-apis-error-registry.md)"]
acs: ["AC-01", "AC-02", "AC-12"]
files_hint:
  - "ConstantLearning/Features/Identity/"
owner: "<TBD Tech Lead>"
estimate: "L"
status: "blocked"
---

# T12 — `startLearnerLanguage` (ідемпотентний старт + активація)

**Шар:** app · **Залежить від:** [T04](./T04-per-language-day-state.md), [T06](./T06-base-course-availability.md), [T11](./T11-read-apis-error-registry.md) · **AC:** AC-01, AC-02, AC-12 · **Оцінка:** L · **Фаза:** 5 (API)

> **Blocked — OQ-4** (owner: Tech Lead): модель конкурентності при старті/перемиканні в різних
> вкладках. Контракт фіксує `language.concurrent_update` (409) як гілку оптимістичної конкурентності;
> точна семантика (xmin-конфлікт клієнту vs server-side retry) — до рішення. [PRD §8 OQ-4](../PRD.md#8-open-questions), [SAD §4](../sad.md).

## Why

US-01/flow 4 ([sad.md §6](../sad.md), [api-sync-report §B.5](../contracts/api-sync-report.md)):
старт мови створює рядок `learner_language_states` і робить мову активною. Мутація **ідемпотентна**
за природним ключем `(learner, target_language)` (AC-12) — без окремого `Idempotency-Key`. Мова без
approved-контенту недоступна (AC-02).

## What

- `POST` `startLearnerLanguage` з `target_language`: INSERT рядка стану + UPDATE
  `learners.active_target_language` під блокуванням рядка (learner, lang).
- Гілки: 200 happy (AC-01); 200 ідемпотентно, якщо вже вивчає (AC-12, стан не чіпається);
  409 `language.not_available`, якщо недоступна (AC-02, активна мова без змін);
  404 `language.unknown` для невідомого коду; 409 `language.concurrent_update` при xmin-конфлікті (OQ-4).
- Старт не чіпає облік інших мов.

## Definition of Done

- [ ] Integration: перший старт створює рядок стану + активує мову + віддає «Сьогодні» з першим заняттям (AC-01).
- [ ] Повторний старт тієї ж мови → 200 без другого рядка, word status/історія без змін (AC-12).
- [ ] Старт мови без approved-контенту → 409 `language.not_available`, активна мова незмінна (AC-02).
- [ ] Конкурентний старт/перемикання розв'язано за рішенням OQ-4; гілка `language.concurrent_update` покрита тестом.
- [ ] Contract-тест на `openapi.yaml` (200/404/409) зелений.
- [ ] PR ≤500 LOC.
