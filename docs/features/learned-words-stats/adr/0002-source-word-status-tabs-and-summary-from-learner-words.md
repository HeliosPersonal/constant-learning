---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-11-03"
feature_size: S
stage: "04-05"
ticket: "TBD"
---

# 0002 — Брати статус, вкладки й зведення з learner_words.status

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** Viacheslav Melnichenko

## Context

Другий ризик фічі ([idea-brief §10](../idea-brief.md#10-risks)): статус і лічильники можуть розходитися. Слово з «5 правильних, 0 неправильних» усе ще лежить у «На повторення», бо статус «Вже пригадую» вимагає двох правильних повторень у різні доби. Якщо фіча переобчислить статус із журналу відповідей за власним правилом, вона розійдеться з екраном «Сьогодні», який читає `learner_words.status`, і learner перестане довіряти цифрам ([idea-brief §8](../idea-brief.md#8-multi-perspective-feedback)). Стан слова (`status`, `consecutive_scheduled_correct`, `lapses`, `first_shown_day`, `last_review_day`, `due_on`) супроводжується планувальником у `learner_words` як проєкція журналу ([ADR-0009](../../personal-trainer-mvp/adr/0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md)).

## Decision drivers

- Статус, вкладки й зведення не повинні суперечити екрану «Сьогодні» ([idea-brief §10](../idea-brief.md#10-risks)).
- Не дублювати правило переходу статусу, яке живе в планувальнику.
- Лічильники вкладок мають бути дешевими ([idea-brief §8](../idea-brief.md#8-multi-perspective-feedback)).

## Considered options

1. **Брати статус, серію й забуте напряму з `learner_words`.** `status` → вкладки `all` / `recalled` / `review` і зведення; `consecutive_scheduled_correct` → `streak`; `lapses` → `forgotten_count`. Одне джерело істини зі «Сьогодні».
2. **Переобчислювати статус із журналу у фічі.** Статистика самодостатня, але фіча дублює правило планувальника; будь-яка розбіжність версій дає суперечність зі «Сьогодні».
3. **Гібрид: вкладки з learner_words, але `streak` рахувати з журналу.** Частково узгоджено, але серія в журналі й у планувальнику може розійтися (додаткова практика, оскарження), знову підриваючи довіру.

Варіант 2 прямо реалізує ризик розбіжності зі «Сьогодні»; варіант 3 лишає серію як окреме джерело неузгодженості.

## Decision outcome

**Chosen:** Option 1. `LearnedWordsStatsService` читає `learner_words` зі `status ∈ {review, recalled}` як множину побачених слів; вкладки (`all` / `recalled` / `review`) і зведення (`seen_count`, `recalled_count`, `review_count`) фільтрують і рахують за `status`. `streak` = `consecutive_scheduled_correct`, `forgotten_count` = `lapses`, дати (`first_shown_day`, `last_review_day`, `due_on`) — теж із `learner_words`. Похідне `is_weak` = `status != recalled` і (`lapses > 0` або `incorrect_count > 0`); `weak_count` рахується тим самим правилом. Лише показ / правильно / неправильно й розбивка приходять із журналу ([ADR-0001](0001-count-only-graded-reviews-minus-successfully-disputed-misses.md)).

## Consequences

**Positive**
- Статус, вкладки й зведення завжди збігаються зі «Сьогодні» — те саме джерело.
- Правило переходу статусу не дублюється; фіча не має власної версії планувальника.
- Слово, позначене «Вже знаю», з'являється в «На повторення» з нулем лічильників без спеціального випадку ([idea-brief §9](../idea-brief.md#9-trade-offs-and-edge-cases)).

**Negative / trade-offs**
- Статистика не самодостатня: без `learner_words` лічильники журналу неповні (немає статусу й серії). Прийнятно — обидві таблиці живуть разом і чистяться разом ([ADR-0003](0003-read-stats-live-with-in-memory-aggregation-no-materialized-table.md)).
- «Пригадую лише після запланованих повторень чи й після додаткового тренування» вирішується планувальником, а не фічею; фіча лише відображає його `status` ([idea-brief §15](../idea-brief.md#15-open-questions)).

**Neutral**
- Вкладка «Усі» показує лише слова з `learner_word` (побачені), тож порядок непоказаних слів курсу не розкривається.

## Related

- [idea-brief](../idea-brief.md) · [SAD](../sad.md) · [контракт openapi](../contracts/openapi.yaml)
- [ADR-0001](0001-count-only-graded-reviews-minus-successfully-disputed-misses.md) · [ADR-0003](0003-read-stats-live-with-in-memory-aggregation-no-materialized-table.md) · [ADR-0009 personal-trainer-mvp](../../personal-trainer-mvp/adr/0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md)
