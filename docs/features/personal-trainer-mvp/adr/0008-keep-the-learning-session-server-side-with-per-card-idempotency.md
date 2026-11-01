---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0008 — Keep the learning session server-side with per-card idempotency

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

Session має до 20 слів ([PRD §5](../PRD.md#5-acceptance-criteria)), продовжується тієї самої доби з наступної картки (AC-24) і закривається опівночі (AC-25). Відповідь не можна втратити чи зарахувати двічі ([PRD §6](../PRD.md#6-non-functional-requirements)), а оцінка залежить від прийнятих варіантів, які не можна розкривати клієнту.

## Decision drivers

- 0 втрачених відповідей і одне зарахування при повторі ([PRD §6](../PRD.md#6-non-functional-requirements))
- Latency p95 оцінки відповіді ≤ 500 ms
- AC-24, AC-25: продовження й межа доби
- Правильні відповіді не потрапляють на клієнт до відповіді

## Considered options

1. **Сервер — джерело істини, картка session як природний ключ** — Session з упорядкованими картками в БД; відповідь — умовне захоплення картки з `pending` в `answered`; відповідь повертає результат і наступну картку.
2. **Offline-first клієнт** — Картки й прийняті варіанти на клієнті, локальна оцінка, черга відповідей у IndexedDB.
3. **Сервер + заголовок `Idempotency-Key`** — Окрема таблиця збережених відповідей за ключем, згенерованим клієнтом.

## Decision outcome

**Chosen:** Option 1. Доменне правило «картку оцінюють один раз» збігається з ключем ідемпотентності, тож окрема таблиця ключів не потрібна; повтор із тим самим вмістом отримує збережений результат, з іншим — відмову.

## Consequences

**Positive**
- Один round trip на картку завдяки поверненню наступної картки
- Оцінка й межі доби лишаються серверними інваріантами

**Negative**
- Без мережі заняття неможливе — потрібен чесний UI помилки
- Кожна нова команда («Вже знаю», content report) потребує власного природного ключа
- Content report з подвійного натискання рахується до ліміту, якщо ключ не додати

**Neutral**
- Session закривається ліниво при читанні після `ends_at_utc`

## Links

- PRD: [PRD](../PRD.md)
- SAD: [SAD](../sad.md) §6
- Related ADR: [ADR-0009](0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md), [ADR-0006](0006-model-learner-time-with-nodatime-and-iana-zones.md)
