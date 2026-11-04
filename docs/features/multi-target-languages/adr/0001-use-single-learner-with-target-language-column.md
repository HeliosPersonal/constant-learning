---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead", "Security Lead"]
updated_at: "2026-11-04"
feature_size: M
stage: "04-05"
ticket: "TBD"
---

# 0001 — Тримати один learner із дискримінатором target_language на таблицях прогресу

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** Viacheslav Melnichenko (Tech Lead), архітектурна сесія

## Context

Тренер сьогодні веде прогрес learner без мовного виміру: `learner_words` ключований `(learner_id, entry_id)`, а стан обліку (word status, повторення, daily new words limit) — один на learner (sad §3, brownfield-скан). Фіча вимагає окремого прогресу на кожну target language (PRD AC-03, AC-05, AC-10). Питання: моделювати learner як одну сутність із мовним виміром на прогресі, чи як окрему одиницю на пару learner+мова (PRD OQ-3). Рішення фундаментальне — від нього залежать усі таблиці прогресу, ключі ізоляції та обсяг міграції.

## Decision drivers

- Quality goal №1 — ізоляція прогресу між мовами (sad §1; PRD §6 «Ізоляція прогресу» = 0 відповідей не до своєї мови).
- Єдина перевірена Telegram-ідентичність на людину (sad §2 Constraints; PRD §6.1) — дублювати її небажано.
- Конвенції міграцій: зеро-даунтайм на наявних таблицях, FK+індекс (sad §2; `.claude/rules/migrations.md`).
- Мінімальна перебудова наявного feature-module моноліту (sad §2).

## Considered options

1. **Один learner + колонка `target_language` на таблицях прогресу** — ідентичність і налаштування learner спільні; прогрес ключується `(learner_id, target_language, …)`; наявні рядки бекфіляться `'pl'`.
2. **Окрема одиниця learner+мова** — кожна пара learner+мова як окремий агрегат із власною ідентичністю/налаштуваннями/добою.

## Decision outcome

**Chosen:** Option 1. Ізоляція досягається складеним ключем `(learner_id, target_language)` без дублювання ідентичності, налаштувань і Telegram-прив'язки; наявні польські дані мігрують бекфілом `'pl'` за зеро-даунтайм патерном. Option 2 дав би «чистішу» фізичну ізоляцію ціною дублювання ідентичності/налаштувань і значно важчої міграції, що суперечить драйверу мінімальної перебудови.

## Consequences

**Positive**
- Ізоляція прогресу — питання ключа й фільтра `(learner_id, target_language)`, тестується інтеграційно (sad §10 QG-1).
- Єдина ідентичність/налаштування learner; додавання мови не дублює обліковий запис людини.
- Наявні польські дані мігрують бекфілом, без переписування learner-сутності.

**Negative**
- Кожен запит прогресу зобов'язаний фільтрувати за `target_language` — пропуск фільтра = витік між мовами; потрібен наскрізний інваріант (sad §8) і тест ізоляції.
- Складені унікальні індекси й FK-індекси на кожну таблицю прогресу розростаються.

**Neutral**
- Перехід до моделі learner+мова пізніше можливий, але потребує розщеплення рядків і бекфілу.
- Форма day-state на мову вирішується окремо — див. [0002](0002-scope-day-state-per-target-language.md).

## Links

- PRD: [PRD.md](../PRD.md) (US-03, AC-03/05/10, OQ-3)
- SAD: [sad.md](../sad.md) §4, §5
- Related ADR: [0002](0002-scope-day-state-per-target-language.md), [0003](0003-place-target-language-on-dictionary-entry.md)
