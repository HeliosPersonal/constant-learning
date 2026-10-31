---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0010 — Version dictionary entries in Postgres with a published pointer

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

PRD §1 робить редакторський словник єдиним джерелом контенту з версіями, історією й відкатом; learner бачить лише `approved` (AC-32), зняття статусу ховає слово (AC-33), зміни застосовуються під час роботи автоматично (AC-35) або рішенням content editor (AC-38), а прогрес learner переживає зміну перекладу (AC-42).

## Decision drivers

- Відкат і історія (PRD §1)
- Автоматичне застосування змін без участі редактора (AC-35)
- Прогрес посилається на стабільний запис (AC-42)
- Поточний `Words` + одноразовий CSV-імпорт непридатні

## Considered options

1. **PostgreSQL як майстер** — Запис зі стабільним id і `published_version_id`; незмінні версії з `validation_status`, джерелами, автором; переклади, прийняті польські варіанти, докази звірки; журнал публікацій; CSV — лише канал імпорту.
2. **Git/CSV як майстер** — Повторний імпорт на деплої, зміни зі звітів переносяться в CSV через PR.
3. **Гібрид** — CSV — майстер пакетної редактури, БД — накладки зі звітів, що зливаються при імпорті.

## Decision outcome

**Chosen:** Option 1. Лише він дає застосувати автоматичне підтвердження без коміту в Git; два вказівники розв'язують AC-32, AC-33, відкат і правило «звіт на розгляді не чіпає запис», а прогрес на `entry_id` виконує AC-42.

## Consequences

**Positive**
- Відкат — пересування вказівника
- Імпорт CSV ідемпотентний: нова версія лише при зміні `content_hash`

**Negative**
- 6–7 таблиць замість однієї; порядок base course треба матеріалізувати й перераховувати при публікації
- CSV у репозиторії застарівають — потрібен експорт з БД у CSV
- `WordImportService` переписується на справжній CSV-парсер

**Neutral**
- Поле прийнятих польських варіантів треба додати до `content-schema.md` (SAD §11)

## Links

- PRD: [[../PRD.md]]
- SAD: [[../sad.md]] §4
- Related ADR: [[0011-verify-content-reports-asynchronously-against-reference-snapshots]], [[0012-search-via-a-precomputed-table-of-folded-word-forms]]
