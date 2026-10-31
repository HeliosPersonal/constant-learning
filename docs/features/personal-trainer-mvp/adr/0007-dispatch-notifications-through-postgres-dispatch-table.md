---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0007 — Dispatch notifications through a Postgres dispatch table

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

Бот надсилає три види повідомлень: reminder у час learner (AC-26), щоденний підсумок content reports о 12:00 у поясі learner (AC-37) і дайджест редактора о 09:00 за Варшавою (AC-43). Не більше одного на дату навіть після зміни поясу (AC-44); поточний розклад — точний збіг хвилини без обліку надісланого.

## Decision drivers

- ≥ 99% нагадувань у межах 2 хв (PRD §6)
- Рівно одне нагадування на дату learner (AC-26, AC-44)
- Без дублікатів після рестартів і з другою реплікою
- Журнал доставки для вимірювання NFR

## Considered options

1. **Скан за `next_reminder_due_utc` + окремий sent-log** — Щохвилинний відбір learner за обчисленою колонкою, ідемпотентність окремою таблицею.
2. **Quartz-тригер на кожного learner з `AdoNetJobStore`** — Персистентні cron-тригери з поясом learner, таблиці `qrtz_*` поза EF-міграціями.
3. **Таблиця `notification_dispatch`** — Рядок на `(recipient_id, kind, local_date)` з унікальним ключем, статусами й `due_at_utc`; planner перераховує, dispatcher кожні 30 с захоплює належні рядки з пропуском заблокованих.

## Decision outcome

**Chosen:** Option 3. Унікальний ключ прямо кодує правило «одне на дату», одна таблиця обслуговує всі три види, а сама таблиця є журналом доставки для NFR; захоплення з пропуском заблокованих рядків виключає дублі між репліками.

## Consequences

**Positive**
- Закривається борг «немає захисту від повторного надсилання»
- Умови перевіряються в момент відправлення (стан session, закриті reports, черга редактора)

**Negative**
- Planner мусить перераховувати рядки при кожній зміні налаштувань, поясу й днів — потрібні тести DST
- Семантика «не більше одного»: збій між надсиланням і позначкою втрачає повідомлення
- Прострочені понад 60 хв рядки позначаються `missed`, а не надсилаються пізно

**Neutral**
- Тик лишається Quartz `RAMJobStore` з `[DisallowConcurrentExecution]` за конвенцією репозиторію
- Відмова бота (користувач заблокував бота) вимикає нагадування learner

## Links

- PRD: [[../PRD.md]]
- SAD: [[../sad.md]] §6
- Related ADR: [[0006-model-learner-time-with-nodatime-and-iana-zones]], [[0001-deliver-trainer-as-backend-service-plus-telegram-mini-app]]
