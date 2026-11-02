---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-11-01"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0004 — Retire the group model without data migration

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

[PRD §1](../PRD.md#1-context) і §3 виводять групову модель з ужитку без перенесення прогресу: дані належать незапущеному пілоту. Групові завдання на `DateTime.Now`, команди й таблиці `ChatRegistrations`, `LearnedWords`, `Words` несумісні з моделлю learner і версіями словника.

## Decision drivers

- Non-goal [PRD §3](../PRD.md#3-non-goals): перенесення прогресу груп
- Нові таблиці словника з версіями несумісні з `Words` ([ADR-0010](0010-version-dictionary-entries-in-postgres-with-a-published-pointer.md))
- Одноразовий CSV-імпорт треба замінити (`architecture-map.md` §Constraints)

## Considered options

1. **Видалити зараз** — Першим завданням прибрати групові завдання, команди й імпорт; таблиці видалити міграцією після `pg_dump`.
2. **Тримати обидві моделі до пілоту** — Групові завдання працюють паралельно з новою моделлю.
3. **Заморозити** — Вимкнути код, але залишити таблиці до кінця пілоту.

## Decision outcome

**Chosen:** Option 1 із уточненням від 2026-11-01: груповий код і таблиці видаляються в одному релізі після перевіреного резервного копіювання та пробного відновлення копії в окрему БД. Дві моделі поруч лишили б два щохвилинні cron, ключі `ChatId` і конфлікт `/start` для deep link з груповими командами.

## Consequences

**Positive**
- Один ідентифікатор learner і одна модель прогресу
- Прибирається технічний борг групових завдань

**Negative**
- Після застосування міграції видалення таблиць git revert сам по собі не відновлює схему й дані; відновлення потребує повернення перевіреної копії БД.
- Розділ «Поточна модель бота» в CONTEXT.md і `docs/COMMANDS.md` стають історичними
- Імпорт редакторського словника потрібен до першого наскрізного сценарію

**Neutral**
- Перевірена копія перед релізом дає точку відновлення; резервні копії зберігаються поза кластером 14 днів.

## Links

- PRD: [PRD](../PRD.md)
- SAD: [SAD](../sad.md) §4
- Related ADR: [ADR-0003](0003-organise-code-into-feature-modules-inside-the-monolith.md), [ADR-0010](0010-version-dictionary-entries-in-postgres-with-a-published-pointer.md)
