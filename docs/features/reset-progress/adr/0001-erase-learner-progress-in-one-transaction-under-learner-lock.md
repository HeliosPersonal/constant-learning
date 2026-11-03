---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-11-03"
feature_size: S
stage: "04-05"
ticket: "TBD"
---

# 0001 — Стерти прогрес learner однією транзакцією під блокуванням learner

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** Viacheslav Melnichenko

## Context

[PRD](../PRD.md) вимагає, щоб learner міг незворотно стерти весь особистий прогрес ([AC-01](../PRD.md#5-acceptance-criteria)), не втративши налаштувань ([AC-06](../PRD.md#5-acceptance-criteria)) і content reports ([AC-10](../PRD.md#5-acceptance-criteria)). Прогрес розподілений між модулями: Learning зберігає стан слів, session, картки й журнал відповідей; Content тримає content reports із привʼязкою до відповіді; Identity володіє learner і його щоденним станом. Відповіді та зміна налаштувань уже серіалізуються блокуванням рядка learner ([ADR-0008](../../personal-trainer-mvp/adr/0008-keep-the-learning-session-server-side-with-per-card-idempotency.md)). Журнал відповідей за [ADR-0009](../../personal-trainer-mvp/adr/0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md) — джерело істини й append-only; скидання — свідомий виняток з цього принципу.

## Decision drivers

- 0 випадків часткового стирання ([PRD §6](../PRD.md#6-non-functional-requirements)).
- Безпечні одночасні відповіді з іншого пристрою ([AC-12](../PRD.md#5-acceptance-criteria)).
- Повторюваність без помилки ([AC-09](../PRD.md#5-acceptance-criteria)).
- Проєктні правила: жорстке видалення замість soft delete, бізнес-логіка в застосунку, а не в БД.

## Considered options

1. **Одна транзакція під блокуванням learner; Identity керує, обробники модулів стирають свої дані** — той самий патерн, що й точка розширення зміни налаштувань; атомарність і серіалізація з відповідями безкоштовні.
2. **Окремі виклики модулів без спільної транзакції** — простіше розділити, але збій посередині лишає часткове стирання, що порушує вимогу атомарності.
3. **Identity видаляє таблиці інших модулів напряму** — одна точка, без порту; життєздатна, але порушує межі модулів ([ADR-0003](../../personal-trainer-mvp/adr/0003-organise-code-into-feature-modules-inside-the-monolith.md)) і спокушає забути нові таблиці.
4. **Архівація перед стиранням (знімок прогресу)** — дозволяє відновлення, але додає нове сховище персональної історії; відкладено ([idea-brief §14](../idea-brief.md#14-parked--rejected-approaches)).

Варіант 2 виключає вимога атомарності ([PRD §6](../PRD.md#6-non-functional-requirements)), варіант 4 — рішення відкласти архів; їх розглянуто для повноти.

## Decision outcome

**Chosen:** Option 1. Identity відкриває транзакцію, блокує рядок learner, викликає обробники (спершу `Content`, потім `Learning`), перевіряє, що даних learner не лишилося, і фіксує зміни разом. Дані видаляються назавжди; поля learner, що належать прогресу (відкладений облік виданих сьогодні слів, маркер завершення курсу), скидаються, а налаштування й профіль не змінюються. У content reports прибирається лише привʼязка до відповіді; самі reports лишаються. Це виняток з append-only принципу журналу з ADR-0009: журнал learner стирається разом з рештою.

## Consequences

**Positive**
- Атомарність і серіалізація з відповідями та зміною налаштувань без нових механізмів.
- Нова схема даних не потрібна: обробники явно видаляють дані в зафіксованому порядку (спершу Content знімає привʼязку reports, потім Learning стирає відповіді), бо каскади працюють лише від рядка learner, який лишається.
- Обробники в модулях виконують лише локальні зміни, тож залежності між модулями не зростають.

**Negative / trade-offs**
- Скидання тримає блокування learner на час стирання (до ≤ 2 000 ms за [PRD §6](../PRD.md#6-non-functional-requirements)); у цей час відповіді того ж learner чекають.
- Відновлення неможливе; стерті дані лишаються лише у зашифрованих резервних копіях до 14 діб.
- Привʼязка content reports до відповідей знімається незворотно.

**Відхилення від правил**
- Мутація не використовує `Idempotency-Key` попри `.claude/rules/openapi.md`: повтор безпечний за станом (порожній прогрес → успіх без змін), як і низка мутацій у [ADR-0008](../../personal-trainer-mvp/adr/0008-keep-the-learning-session-server-side-with-per-card-idempotency.md).

**Neutral**
- Модуль, що додасть нові дані про прогрес learner, має підключити свій обробник скидання; це контролюється тестом повноти скидання.

## Related

- [PRD reset-progress](../PRD.md) · [SAD](../sad.md) · [idea-brief](../idea-brief.md)
- [ADR-0008](../../personal-trainer-mvp/adr/0008-keep-the-learning-session-server-side-with-per-card-idempotency.md) · [ADR-0009](../../personal-trainer-mvp/adr/0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md)
