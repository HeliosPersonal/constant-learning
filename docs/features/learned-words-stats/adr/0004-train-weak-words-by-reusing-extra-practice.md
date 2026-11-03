---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-11-03"
feature_size: S
stage: "04-05"
ticket: "TBD"
---

# 0004 — Тренувати слабкі слова наявним extra-practice, без нового ендпоінта

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** Viacheslav Melnichenko

## Context

Approach C ([idea-brief §7](../idea-brief.md#7-strategic-approaches)) обіцяє вхід у тренування зі «Слабких слів» одним дотиком. Ризик idea-brief: окремий вхід може обійти правила додаткового тренування (одна практика на слово за добу, виключення control repetition і слів, отриманих сьогодні), бо зараз ці правила описані лише в документації ([idea-brief §10](../idea-brief.md#10-risks)). Додаткова практика вже реалізована в [unlimited-learning](../../unlimited-learning/sad.md) як типізоване заняття `POST /api/v1/sessions/extra-practice` з відбором слів і правилами на сервері ([ADR-0001 unlimited-learning](../../unlimited-learning/adr/0001-run-extras-as-typed-sessions-over-the-existing-session-pipeline.md), [ADR-0002 unlimited-learning](../../unlimited-learning/adr/0002-model-extra-practice-as-a-third-exercise-kind-in-the-scheduler.md)). Вхід у тренування був залежністю фічі від unlimited-learning ([idea-brief §12](../idea-brief.md#12-feasibility--claude-proposed), [idea-brief §15](../idea-brief.md#15-open-questions)).

## Decision drivers

- Правила додаткового тренування мають діяти лише в одному місці, щоб вхід зі статистики не обходив їх ([idea-brief §10](../idea-brief.md#10-risks)).
- Фіча — лише читання ([ADR-0003](0003-read-stats-live-with-in-memory-aggregation-no-materialized-table.md)); додавати мутацію до її поверхні небажано.
- Менша поверхня API — менше тестів і менше розсинхрону з планувальником.

## Considered options

1. **Повторно використати наявний `POST /api/v1/sessions/extra-practice`.** Екран «Мої слова» лише веде до вже наявної дії; відбір слів і правила (одна практика на слово за добу, виключення, межа доби) лишаються на сервері unlimited-learning.
2. **Новий ендпоінт тренування зі статистики (наприклад, тренувати саме показані «слабкі» слова).** Дав би точний контроль над набором, але дублює відбір і правила extra-practice; ризик розійтися з ними й обійти «одна практика за добу».
3. **Дозволити фічі самій створювати session зі слабких слів.** Найтісніша інтеграція, але фіча перестає бути read-only й бере на себе правила планувальника — пряме порушення меж модулів і ризику idea-brief.

Варіанти 2 і 3 дублюють або обходять правила додаткового тренування — реалізують ризик idea-brief; варіант 3 ще й ламає read-only-природу фічі.

## Decision outcome

**Chosen:** Option 1. Фіча не додає навчального ендпоінта. Кнопка «Потренувати слабкі слова» на екрані «Мої слова» викликає наявний `POST /api/v1/sessions/extra-practice`; `is_weak` і сортування `weakest` лише допомагають learner побачити слабкі слова, а який саме набір увійде в практику, вирішує відбір unlimited-learning на сервері. Якщо на момент релізу extra-practice ще недоступний, вхід у тренування можна приховати на клієнті без зміни backend статистики — залежність, а не блокер списку ([idea-brief §12](../idea-brief.md#12-feasibility--claude-proposed)).

## Consequences

**Positive**
- Правила додаткового тренування лишаються в одному місці; вхід зі статистики не може їх обійти.
- Фіча лишається чистим читанням; її поверхня API — лише три GET.
- Нуль дублювання відбору слів і логіки session.

**Negative / trade-offs**
- Набір слів у практиці визначає відбір extra-practice, а не рівно ті «слабкі» рядки, що бачить learner у списку; це свідомо, щоб не дублювати правила.
- Вхід у тренування залежить від стану unlimited-learning; за його відсутності кнопку ховає клієнт.

**Neutral**
- Зв'язок зі статистикою — лише навігаційний на клієнті; backend статистики про extra-practice нічого не знає.

## Related

- [idea-brief](../idea-brief.md) · [SAD](../sad.md) · [контракт openapi](../contracts/openapi.yaml)
- [ADR-0003](0003-read-stats-live-with-in-memory-aggregation-no-materialized-table.md) · [ADR-0001 unlimited-learning](../../unlimited-learning/adr/0001-run-extras-as-typed-sessions-over-the-existing-session-pipeline.md) · [ADR-0002 unlimited-learning](../../unlimited-learning/adr/0002-model-extra-practice-as-a-third-exercise-kind-in-the-scheduler.md)
