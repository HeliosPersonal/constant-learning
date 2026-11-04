---
id: T10
title: "Ізоляція обліку прогресу за (learner, мова)"
layer: "domain"
deps: ["[T04](./T04-per-language-day-state.md)", "[T08](./T08-migrate-learner-words-answer-events.md)", "[T09](./T09-migrate-sessions-reports.md)"]
acs: ["AC-04", "AC-05", "AC-10"]
files_hint:
  - "ConstantLearning/Features/Learning/"
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T10 — Ізоляція обліку прогресу за `(learner, мова)`

**Шар:** domain · **Залежить від:** [T04](./T04-per-language-day-state.md), [T08](./T08-migrate-learner-words-answer-events.md), [T09](./T09-migrate-sessions-reports.md) · **AC:** AC-04, AC-05, AC-10 · **Оцінка:** L · **Фаза:** 4 (Learning)

## Why

Це серце фічі й **ризик №1** ([SAD §8](../sad.md), [test-plan.md §Ізоляція](../test-plan.md)):
кожен запит прогресу/квоти має фільтрувати за `(learner_id, target_language)`, інакше відповідь
зараховується не своїй мові (AC-04), ліміти/«Вже знаю» змішуються (AC-05) або learner бачить чужий
прогрес (AC-10). Денормалізований `target_language` (T08/T09) має використовуватися в кожному фільтрі.

## What

- Протягнути `target_language` у фільтри: словесний прогрес (word status, due), складання session,
  оцінку відповіді, облік дії «Вже знаю», добовий ліміт нових слів — усі за `(learner, активна мова)`.
- Відповідь зараховується лише `target_language` своєї session; картка не переноситься між мовами (AC-04).
- Інваріант: `target_language` рядка прогресу = `target_language` його `entry` — app-перевірка + тест.
- Крос-learner приховування (AC-10): прогрес чужого learner будь-якою мовою не повертається й не існує для запитувача.

## Definition of Done

- [ ] Integration-тест: відповідь у мові A не змінює жоден лічильник мови B (0 витоків — NFR ізоляції).
- [ ] Тест: вичерпаний ліміт/«Вже знаю» у A не впливає на видачу B того ж learner (AC-05).
- [ ] Тест: незавершена session A лишається доступною після перемикання на B; картки не мігрують (AC-04).
- [ ] Тест: learner 2 не бачить прогрес learner 1 у жодній мові (AC-10, приховування існування).
- [ ] Інваріант «мова рядка = мова entry» покрито тестом; жоден гарячий запит не робить join заради мови.
- [ ] PR ≤500 LOC.
