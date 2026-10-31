---
status: Draft
owner: "Viacheslav Melnichenko"
reviewers: []
updated_at: "2026-10-31"
feature_size: L
---

# Events — personal-trainer-mvp

Асинхронний контракт для потоків з `sad.md` §6. Окремого брокера немає: «канали» — це таблиці-черги в PostgreSQL, з яких фонові тики бекенду захоплюють рядки з пропуском заблокованих (ADR-0007, ADR-0011). Кожна подія відповідає повідомленню enqueue/deliver у діаграмі §6. Зовнішніх споживачів немає; контракт фіксує, що пишуть і читають модулі одного процесу.

## Channel: `verification_jobs`

Потік §6 «Content report і автоматична звірка».

### `content_report.verification_requested.v1`

- **Producer:** модуль `Content`, операція `createContentReport`, коли report стосується перекладу або транскрипції й має `proposed_value` (AC-35).
- **Consumer:** фоновий тик звірки модуля `Content`.
- **Payload** (рядок `verification_jobs`, `data-model.md`):

| Field | Type | Origin |
|---|---|---|
| `id` | UUID | `verification_jobs.id` |
| `content_report_id` | UUID | `verification_jobs.content_report_id` — ключ ідемпотентності (UNIQUE) |
| `status` | `pending` / `running` / `done` / `dead` | `verification_jobs.status` |
| `attempts` | integer | `verification_jobs.attempts` |
| `next_attempt_at` | timestamptz | `verification_jobs.next_attempt_at` |

- **Idempotency:** одна задача на report (UNIQUE `content_report_id`); оброблена задача пропускається.
- **Retry:** до 3 спроб з експоненційною затримкою при недоступності довідкового джерела.
- **Dead-letter:** після 3 невдач `status = dead`, report іде в чергу content editor з причиною «джерело недоступне».
- **Outcome:** підтверджено → нова версія запису й закриття report (`applied`); не підтверджено → `in_editor_queue`.

## Channel: `notification_dispatches`

Потоки §6 «Critical flow 2: щоденне нагадування» і «Щоденні підсумки й дайджест».

### `notification.dispatch_due.v1`

- **Producer:** planner модуля `Notifications` — після збереження налаштувань, після відправлення і під час щоденного прогону.
- **Consumer:** dispatcher модуля `Notifications` (тик кожні 30 с).
- **Payload** (рядок `notification_dispatches`):

| Field | Type | Origin |
|---|---|---|
| `id` | UUID | `notification_dispatches.id` |
| `recipient_telegram_user_id` | bigint | `notification_dispatches.recipient_telegram_user_id` |
| `learner_id` | UUID або null | `notification_dispatches.learner_id` |
| `kind` | `reminder` / `report_outcomes` / `editor_digest` | `notification_dispatches.kind` |
| `local_date` | date | `notification_dispatches.local_date` |
| `due_at` | timestamptz | `notification_dispatches.due_at` |
| `status` | `pending` / `sending` / `sent` / `skipped` / `missed` / `failed` | `notification_dispatches.status` |

- **Idempotency:** UNIQUE (`recipient_telegram_user_id`, `kind`, `local_date`) — не більше одного відправлення на дату (AC-26, AC-44).
- **Умови в момент відправлення:** `reminder` — пропуск, якщо session цієї доби завершено або learner відповідав менше 30 хв тому (AC-26, OQ SAD §11); `report_outcomes` — пропуск без закритих reports за минулу добу (AC-37); `editor_digest` — пропуск без reports без рішення (AC-43).
- **Retry:** до 3 спроб при тимчасовій помилці Telegram до прийняття повідомлення; семантика «не більше одного» (ADR-0007).
- **Dead-letter:** після 3 невдач `status = failed`; відмова через заблокованого бота вимикає `learners.can_message`.
- **Missed:** рядок, прострочений понад 60 хв (наприклад, після простою), позначається `missed` і не надсилається.
