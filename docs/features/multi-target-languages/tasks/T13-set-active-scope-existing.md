---
id: T13
title: "setActiveLanguage + scoping наявних ендпоінтів за активною мовою"
layer: "app"
deps: ["[T10](./T10-progress-isolation.md)", "[T12](./T12-start-learner-language.md)"]
acs: ["AC-03", "AC-04", "AC-07"]
files_hint:
  - "ConstantLearning/Features/Identity/"
  - "ConstantLearning/Features/Learning/"
  - "ConstantLearning/Features/Content/"
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "done"
---

# T13 — `setActiveLanguage` + scoping наявних ендпоінтів за активною мовою

**Шар:** app · **Залежить від:** [T10](./T10-progress-isolation.md), [T12](./T12-start-learner-language.md) · **AC:** AC-03, AC-04, AC-07 · **Оцінка:** L · **Фаза:** 5 (API)

## Why

US-02/flow 1 ([sad.md §6](../sad.md)): перемикання активної мови має показати «Сьогодні», заняття,
словник і налаштування **нової** мови, лишивши облік попередньої незмінним (AC-03). Наявні
reference-ендпоінти (today, sessions, dictionary) — **крос-файлова дельта**
([api-sync-report §B.4/B.5](../contracts/api-sync-report.md)): сервер скоупить їх за активною мовою
learner, тіло не змінюється.

## What

- `PUT`/`POST` `setActiveLanguage` з `target_language`: UPDATE `learners.active_target_language`;
  409 `language.not_studying`, якщо learner не вивчає цю мову (невідомий код теж → 409
  `language.not_studying`, existence-hiding; 404 `language.unknown` є лише на `startLearnerLanguage` —
  див. [openapi.yaml](../contracts/openapi.yaml)).
- Серверне scoping наявних ендпоінтів (today / session / dictionary / settings) за
  `active_target_language`: усі читання/мутації йдуть у вимірі активної мови (AC-03).
- Незавершена session попередньої мови лишається доступною тієї ж доби (AC-04).
- Порожній початковий стан свіжообраної мови (AC-07) — лічильники інших мов не показуються.

## Definition of Done

- [x] `setActiveLanguage` перемикає активну мову; 409 `language.not_studying` для невивченої (і для невідомого коду — existence-hiding, без 404).
- [x] Integration: після перемикання today/session/dictionary віддають стан нової мови; облік старої незмінний і доступний назад (AC-03).
- [x] Тест: незавершена session старої мови продовжується тієї ж доби learner (AC-04).
- [x] Тест: щойно обрана мова показує порожній початковий стан без лічильників інших мов (AC-07).
- [x] Contract-тест на `openapi.yaml`; крос-файлові дельти зафіксовано для злиття в reference-контракт.
- [x] PR ≤500 LOC.
