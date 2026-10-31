---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0004 — Retire the group model without data migration

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

PRD §1 і §3 виводять групову модель з ужитку без перенесення прогресу: дані належать незапущеному пілоту. Групові завдання на `DateTime.Now`, команди й таблиці `ChatRegistrations`, `LearnedWords`, `Words` несумісні з моделлю learner і версіями словника.

## Decision drivers

- Non-goal PRD §3: перенесення прогресу груп
- Нові таблиці словника з версіями несумісні з `Words` (ADR-0010)
- Одноразовий CSV-імпорт треба замінити (`architecture-map.md` §Constraints)

## Considered options

1. **Видалити зараз** — Першим завданням прибрати групові завдання, команди й імпорт; таблиці видалити міграцією після `pg_dump`.
2. **Тримати обидві моделі до пілоту** — Групові завдання працюють паралельно з новою моделлю.
3. **Заморозити** — Вимкнути код, але залишити таблиці до кінця пілоту.

## Decision outcome

**Chosen:** Option 1, з видаленням таблиць наступним релізом після вимкнення коду. Дві моделі поруч лишили б два щохвилинні cron, ключі `ChatId` і конфлікт `/start` для deep link з груповими командами.

## Consequences

**Positive**
- Один ідентифікатор learner і одна модель прогресу
- Прибирається технічний борг групових завдань

**Negative**
- Відкату до групового бота як продукту немає, лише git revert
- Розділ «Поточна модель бота» в CONTEXT.md і `docs/COMMANDS.md` стають історичними
- Імпорт редакторського словника потрібен до першого наскрізного сценарію

**Neutral**
- `pg_dump` перед видаленням зберігає дані для аудиту

## Links

- PRD: [[../PRD.md]]
- SAD: [[../sad.md]] §4
- Related ADR: [[0003-organise-code-into-feature-modules-inside-the-monolith]], [[0010-version-dictionary-entries-in-postgres-with-a-published-pointer]]
