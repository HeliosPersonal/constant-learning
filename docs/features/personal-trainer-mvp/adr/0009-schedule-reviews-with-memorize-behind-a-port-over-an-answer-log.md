---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0009 — Schedule reviews with MEMORIZE behind a port over an answer log

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

`docs/IDEAS.md` обирає MEMORIZE для адаптивних інтервалів; PRD §5 задає межі (перше повторення наступної доби, після mistake — наступної доби, довший інтервал не раніше ніж через 2 доби, control repetition через 7 діб). Даних для підбору параметрів немає до пілоту (PRD OQ-4).

## Decision drivers

- Рішення IDEAS про MEMORIZE
- Правила інтервалів PRD §5, AC-18–AC-23
- Перенастроювання параметрів після пілоту (OQ-4)
- Відкат нейтралізованої відповіді (AC-17)

## Considered options

1. **MEMORIZE за портом `IReviewScheduler` + append-only журнал відповідей** — Швидкість забування на пару learner–слово, вибірка дати з фіксованим seed, межі PRD поверх; журнал — джерело істини, стан слова — проєкція.
2. **Детермінована драбина інтервалів** — Наприклад 1, 3, 7, 16, 35 діб; стан — лише щабель.
3. **FSRS зі стандартними параметрами** — Stability і difficulty, бінарна оцінка.

## Decision outcome

**Chosen:** Option 1. Він виконує рішення IDEAS, а журнал із `state_before` і `scheduler_version` дає змогу перенастроїти параметри чи замінити алгоритм на FSRS повторним прогоном після пілоту; межі PRD накладаються зверху.

## Consequences

**Positive**
- Стан слова перебудовується з журналу
- Нейтралізація AC-17 — окрема подія з відновленням `state_before`

**Negative**
- На пілоті інтервали здебільшого впираються в межі PRD, тож перевагу MEMORIZE не доведено
- Стохастичність ускладнює пояснення «чому слово сьогодні» — потрібні тести з фіксованим seed
- Журнал зі знімками збільшує обсяг даних

**Neutral**
- Пріоритет у session — прострочення відносно інтервалу, за рівності — порядок base course

## Links

- PRD: [[../PRD.md]]
- SAD: [[../sad.md]] §4
- Related ADR: [[0008-keep-the-learning-session-server-side-with-per-card-idempotency]]
