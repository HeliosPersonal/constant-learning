---
id: T04
title: "Per-language стан доби learner (ILearnerDayStateService)"
layer: "domain"
deps: ["[T03](./T03-migrate-identity-day-state.md)"]
acs: ["AC-04", "AC-05", "AC-07"]
files_hint:
  - "ConstantLearning/Features/Identity/"
  - "ConstantLearning.Tests/"
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T04 — Per-language стан доби learner (`ILearnerDayStateService`)

**Шар:** domain · **Залежить від:** [T03](./T03-migrate-identity-day-state.md) · **AC:** AC-04, AC-05, AC-07 · **Оцінка:** L · **Фаза:** 2 (Identity)

## Why

[ADR-0002](../adr/0002-scope-day-state-per-target-language.md) переносить стан доби з `learners`
у `learner_language_states`: блокування береться на рядок **(learner_id, target_language)**, не на
learner. Це змінює конвенцію `ILearnerDayStateService` (SAD §5) і робить видачу нових слів та
обчислення доби (зі зміною поясу) незалежними на мову — підґрунтя AC-04/AC-05/AC-07.

## What

- Переписати `ILearnerDayStateService` на ключ `(learner_id, target_language)`: читання/просування
  доби, обчислення меж зі зміною часового поясу — з рядка `learner_language_states`.
- Dual-write проміжок: сервіс пише в нову таблицю (стара на `learners` лишається до contract-фази).
- Обчислення `new_words_available_from`, `current_day_*`, `course_completed_at` — per-language.
- Блокування рядка день-стану під час мутації (узгодити з конкурентністю T12 / OQ-4).

## Definition of Done

- [ ] Unit-тести обчислення доби зі зміною поясу — зелені для довільної мови (не лише `pl`).
- [ ] Integration-тест: просування доби мови A не торкає рядок день-стану мови B того ж learner (AC-05).
- [ ] Блокування береться на `(learner_id, target_language)`; паралельна мутація різних мов не серіалізується зайве.
- [ ] Порожній стан нового рядка мови дає коректний «перший день» (AC-07).
- [ ] PR ≤500 LOC.
