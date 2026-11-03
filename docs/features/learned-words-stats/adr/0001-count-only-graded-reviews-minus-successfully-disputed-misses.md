---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-11-03"
feature_size: S
stage: "04-05"
ticket: "TBD"
---

# 0001 — Рахувати лише зараховані повторення, помилки за винятком успішно оскаржених

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** Viacheslav Melnichenko

## Context

Найкритичніший ризик фічі ([idea-brief §10](../idea-brief.md#10-risks)): якщо «неправильно» рахувати на сирому журналі, воно включить скасовані оскарження, відповіді-синоніми, разову перевірку нового слова й помилки, які тренер пізніше визнав правильними. Learner побачить, що статистика суперечить його досвіду — мовчки, у кожного слова. Окремого значення «показано» в журналі немає ([idea-brief §9](../idea-brief.md#9-trade-offs-and-edge-cases)). Журнал відповідей — джерело істини ([ADR-0009](../../personal-trainer-mvp/adr/0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md)): кожна спроба — рядок `answer_events` з `exercise_kind` і `outcome`; успішно оскаржена помилка додає рядок у `answer_neutralizations`.

## Decision drivers

- Лічильники мають збігатися з тим, як тренер оцінював learner ([idea-brief §10](../idea-brief.md#10-risks)).
- Діакритична помилка ≠ «не знаю слова» — для польської важливо показати розбивку ([idea-brief §8](../idea-brief.md#8-multi-perspective-feedback)).
- Правило має читатися детерміновано з наявних таблиць, без нових колонок і без запису.

## Considered options

1. **Рахувати лише зараховані види вправ, помилки — за винятком успішно оскаржених.** Показ = будь-яка спроба за `exercise_kind ∈ {review, control_repetition, extra_practice}`; правильно = `outcome = correct`; неправильно = `mistake_typo | mistake_diacritic | dont_know`, анти-join `answer_neutralizations`; синоніми й інші нейтральні результати — лише у показ; разова перевірка нового слова (`new_word_check`) не входить узагалі.
2. **Рахувати кожну експозицію на сирому журналі.** Найпростіше, один `GROUP BY`, але включає оскаржені помилки, синоніми й перевірку нового слова; статистика суперечить досвіду learner — прямо реалізований ризик idea-brief.
3. **Рахувати неправильно як «усе, що не correct».** Проміжний варіант, але нейтральний синонім потрапляє в помилки, а оскаржену помилку не можна відрізнити.

Варіант 2 виключає найкритичніший ризик фічі; варіант 3 так само змішує нейтральні відповіді з помилками й не враховує оскарження.

## Decision outcome

**Chosen:** Option 1. `LearnedWordsStatsService` завантажує `answer_events` learner лише за `GradedKinds = {review, control_repetition, extra_practice}`, бере множину `answer_event_id` із `answer_neutralizations` для цих подій і в пам'яті за один прохід рахує на слово: `shown` (кожна спроба), `correct` (`outcome = correct`), `incorrect` і розбивку (`mistake_typo` / `mistake_diacritic` / `dont_know`, лише якщо подію не оскаржено успішно). `synonym` та інші нейтральні результати входять лише в `shown`. Разова перевірка нового слова (`new_word_check`) не є зарахованим видом і не рахується зовсім. Серія (`streak`) і забуте (`forgotten_count`) не виводяться з подій — вони з `learner_words` ([ADR-0002](0002-source-word-status-tabs-and-summary-from-learner-words.md)).

## Consequences

**Positive**
- Лічильники збігаються з оцінкою тренера: оскаржена помилка зникає, синонім не карає, перевірка нового слова не роздуває покази й помилки.
- Розбивка помилок (друк / діакритика / «не знаю») доступна без нових даних — важливо для польської.
- Правило читається з наявних таблиць без запису й без нових колонок.

**Negative / trade-offs**
- Множину видів вправ і нейтральних результатів треба тримати синхронною з планувальником; новий вид вправи вимагає явного рішення, рахувати його чи ні — потрібен тест.
- Анти-join `answer_neutralizations` робиться в пам'яті на запит; на масштабі пілоту це дешево ([ADR-0003](0003-read-stats-live-with-in-memory-aggregation-no-materialized-table.md)).

**Neutral**
- «Показ» лишається похідним значенням, а не збереженою колонкою; визначення живе в коді фічі.

## Related

- [idea-brief](../idea-brief.md) · [SAD](../sad.md) · [контракт openapi](../contracts/openapi.yaml)
- [ADR-0002](0002-source-word-status-tabs-and-summary-from-learner-words.md) · [ADR-0003](0003-read-stats-live-with-in-memory-aggregation-no-materialized-table.md) · [ADR-0009 personal-trainer-mvp](../../personal-trainer-mvp/adr/0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md)
