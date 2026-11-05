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
status: "done"
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

- [x] Integration: перший старт створює рядок стану (власні дефолти — свіжа доба, стартовий ліміт) + активує мову + віддає `LearnerLanguage` з `201` (AC-01).
- [x] Повторний старт тієї ж мови → 200 без другого рядка, стан без змін (AC-12).
- [x] Старт мови без approved-контенту → 409 `language.not_available`, активна мова незмінна (AC-02).
- [x] Конкурентний старт/перемикання: `xmin`/PK-конфлікт → `409 language.concurrent_update` покрито тестом (OQ-4).
- [x] Contract-тест на `openapi.yaml` (201/200/401/404/409) зелений.
- [x] PR ≤500 LOC.

## Notes

- Контракт (`openapi.yaml`, джерело істини) повертає `LearnerLanguage` з `201` на перший старт і `200` на
  повторний — не представлення «Сьогодні» (getToday лишається окремим викликом; формулювання «віддає
  «Сьогодні»» в чернетці передувало фіксації контракту).
- Невалідне/порожнє тіло трактується як `language.unknown` (404): контракт не визначає 400 для цієї операції;
  `[ApiController]`-автовідповідь 400 свідомо обійдено ручним парсингом тіла.
- `learner.not_onboarded` (404) повертається, коли learner відсутній або онбординг не завершено —
  узгоджено з `listLearnerLanguages` та рештою мутацій Identity.
- Рядок стану нової мови створюється зі свіжими дефолтами **до** першого `getToday`, тож засів зі стану
  доби learner у `LearnerDayStateService` для en/de/nl більше не спрацьовує (закрито TODO(T12)).
