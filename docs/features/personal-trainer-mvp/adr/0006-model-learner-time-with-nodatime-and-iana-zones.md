---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0006 — Model learner time with NodaTime and IANA zones

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

[PRD §5](../PRD.md#5-acceptance-criteria) визначає добу learner як календарну дату в його поясі з урахуванням DST; від неї залежать денний ліміт (AC-06, AC-07), межа session (AC-25), нагадування (AC-26, AC-44) і вікна KPI. Поточний код використовує `DateTime.Now` контейнера.

## Decision drivers

- Доба learner і DST ([PRD §5](../PRD.md#5-acceptance-criteria), AC-06, AC-07, AC-25, AC-44)
- Тестованість межових випадків часу
- Пояс приходить з клієнта як IANA id

## Considered options

1. **NodaTime 3.x** — `IClock`, tzdb, `LocalDate`, `ZonedDateTime`, плагін Npgsql NodaTime, `FakeClock` у тестах.
2. **BCL `TimeZoneInfo` + `TimeProvider`** — IANA id через ICU, ручна обробка неіснуючого й подвійного часу.

## Decision outcome

**Chosen:** Option 1. Явні `LocalDate` і резолвери неіснуючого чи подвійного часу роблять правила PRD прямими: межі доби через `AtStartOfDay`, кінець доби видачі нових слів зберігається як UTC-момент (AC-07), новий ліміт діє з початку наступної доби в новому поясі (AC-44).

## Consequences

**Positive**
- Межі доби й DST перевіряються тестами з `FakeClock`
- Пояс клієнта перевіряється за tzdb; невідомий — `Europe/Warsaw` з позначкою для AC-27

**Negative**
- Нова залежність і NodaTime-типи в схемі
- Версію tzdb треба оновлювати разом із пакетом

**Neutral**
- Session зберігає свою `learner_day` і `ends_at_utc` на момент створення, тож опівнічне завдання не потрібне

## Links

- PRD: [PRD](../PRD.md)
- SAD: [SAD](../sad.md) §8
- Related ADR: [ADR-0007](0007-dispatch-notifications-through-postgres-dispatch-table.md), [ADR-0008](0008-keep-the-learning-session-server-side-with-per-card-idempotency.md)
