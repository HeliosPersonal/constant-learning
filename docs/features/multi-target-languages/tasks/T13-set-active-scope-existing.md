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
status: "todo"
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
  409 `language.not_studying`, якщо learner не вивчає цю мову; 404 `language.unknown`.
- Серверне scoping наявних ендпоінтів (today / session / dictionary / settings) за
  `active_target_language`: усі читання/мутації йдуть у вимірі активної мови (AC-03).
- Незавершена session попередньої мови лишається доступною тієї ж доби (AC-04).
- Порожній початковий стан свіжообраної мови (AC-07) — лічильники інших мов не показуються.

## Definition of Done

- [ ] `setActiveLanguage` перемикає активну мову; 409 `language.not_studying` для невивченої; 404 для невідомої.
- [ ] Integration: після перемикання today/session/dictionary віддають стан нової мови; облік старої незмінний і доступний назад (AC-03).
- [ ] Тест: незавершена session старої мови продовжується тієї ж доби learner (AC-04).
- [ ] Тест: щойно обрана мова показує порожній початковий стан без лічильників інших мов (AC-07).
- [ ] Contract-тест на `openapi.yaml`; крос-файлові дельти зафіксовано для злиття в reference-контракт.
- [ ] PR ≤500 LOC.
