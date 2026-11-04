---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-11-04"
feature_size: M
stage: "04-05"
ticket: "TBD"
---

# 0002 — Вести власний стан доби learner на кожну target language

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** Viacheslav Melnichenko (Tech Lead), архітектурна сесія

## Context

Стан доби learner (`CurrentDayDate`, `CurrentDayStartsAt/EndsAt/OriginalEndsAt`, `CurrentDayTimeZoneAtStart`, `NewWordsAvailableFrom`, `DailyNewWordsLimit`/`PendingDailyNewWordsLimit`, `CourseCompletedAt`) сьогодні — один набір полів на learner у модулі `Identity`, під блокуванням рядка learner на транзакцію (`ILearnerDayStateService`, brownfield-скан). Видача нових слів у межах daily new words limit прив'язана до цього єдиного вікна доби. Фіча вимагає окремого обліку видачі нових слів і початкового стану на кожну мову (PRD AC-05, AC-07). Питання (PRD OQ-1): спільне вікно доби з per-language лічильником видачі, чи повне per-language вікно доби.

## Decision drivers

- Quality goal №1 — повна незалежність обліку між мовами (sad §1; AC-05: вичерпання ліміту в одній мові не зачіпає іншу).
- AC-07 — зрозумілий початковий стан нової мови без лічильників інших мов (PRD §5).
- Конвенція блокування: день-стан береться під блокування рядка на транзакцію (sad §2; brownfield-скан).

## Considered options

1. **Спільне вікно доби learner + per-language лічильник виданих нових слів** — одне вікно доби (наявні `CurrentDay*`), окремий лише лічильник видачі на мову (PRD default).
2. **Повне per-language вікно доби** — власні `CurrentDay*`, `NewWordsAvailableFrom`, `DailyNewWordsLimit`, `CourseCompletedAt` на кожну пару learner+мова.

## Decision outcome

**Chosen:** Option 2. Видача нових слів (AC-05), наявність нових слів (`NewWordsAvailableFrom`) і завершення курсу (`CourseCompletedAt`) стають повністю незалежними на мову, що найпрямолінійніше задовольняє quality goal №1 та AC-07. Ціна — ширша перебудова day-state у `Identity`: ці поля виносяться в per-`(learner_id, target_language)` рядок, а не лишаються на learner (міграційний ризик зафіксовано в sad §11).

## Consequences

**Positive**
- Облік доби, ліміту й курсу кожної мови повністю ізольований — немає спільного вікна, що «тече» між мовами.
- AC-07 (чистий початковий стан) тривіальний: нова мова = новий per-language рядок day-state.
- `DailyNewWordsLimit` може відрізнятися на мову (відкриває OQ-1(а) без подальшої перебудови).

**Negative**
- Більша перебудова `Identity`: 5+ полів доби + таймзонна логіка переносяться на per-language рядок; блокування береться на рядок `(learner_id, target_language)`, а не learner.
- Важча міграція наявних польських learner — бекфіл day-state у новий per-language рядок `'pl'`.

**Neutral**
- Спільна «доба learner» (AC-04) лишається орієнтиром для UX продовження session тієї самої доби; узгодження спільної доби з per-language вікнами — деталь реалізації в §6.
- Таймзона береться з `CurrentDayTimeZoneAtStart` на мову — поведінка при зміні tz між мовами однакова з наявною.

## Links

- PRD: [[../PRD.md]] (US-03, AC-04/05/07, OQ-1)
- SAD: [[../sad.md]] §4, §5, §11
- Related ADR: [[0001-use-single-learner-with-target-language-column]]
