---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-11-03"
feature_size: S
stage: "04-05"
ticket: "TBD"
---

# 0001 — Вести додаткові слова й практику як типізовані заняття поверх наявного конвеєра session

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** Viacheslav Melnichenko

## Context

[PRD](../PRD.md) вводить дві дії після виконання плану дня: «Ще одне слово» (extra new word) і «Потренувати вивчені» (extra practice). Вони мають бути безпечні при подвійному натисканні й двох пристроях ([AC-05](../PRD.md#ac-05---domain-invariant)), закриватися в кінці доби learner ([AC-07](../PRD.md#ac-07---domain-invariant), [AC-20](../PRD.md#ac-20---domain-invariant)), не створювати completed session ([AC-08](../PRD.md#ac-08---happy-path), [AC-18](../PRD.md#ac-18---domain-invariant)) і не змінювати ліміт нових слів ([AC-02](../PRD.md#ac-02---domain-invariant)). Нині все це забезпечує session: одна активна session на learner, ідемпотентна відповідь на картку, закриття на опівночі й перевірка відповіді ([ADR-0008](../../personal-trainer-mvp/adr/0008-keep-the-learning-session-server-side-with-per-card-idempotency.md)). Журнал відповідей прив'язує відповідь до картки session ([ADR-0009](../../personal-trainer-mvp/adr/0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md)).

## Decision drivers

- Не дублювати перевірену ідемпотентність і закриття доби (мало місць для помилок, S за зусиллям).
- Додаткові дії не повинні впливати на лічильник completed session, нагадування й ліміт 20 слів плану.
- Мінімальна міграція, сумісна з правилами zero-downtime (`.claude/rules/migrations.md`).
- Фронтенд має повторно використати екран заняття (картка, перевірка, результат).

## Considered options

1. **Типізовані заняття: колонка kind у `learning_sessions` (plan / extra_word / extra_practice)** — вся наявна машинерія працює без дубля; extra відрізняється типом і правилами добору.
2. **Окремі таблиці карток extra** — не чіпає запитів про session, але дублює ідемпотентність, закриття доби й відображення картки; фронтенд мав би друге «заняття».
3. **Без session: відповіді extra пишуться прямо в журнал** — найменше сутностей, але втрачається захист «одна відповідь на картку», потрібен новий механізм повтору й відновлення картки після закриття застосунку.

Варіант 3 виключає вимога ідемпотентності відповіді ([PRD §6](../PRD.md#6-non-functional-requirements)), якби не дублювати її вручну; варіант 2 дублює наявну логіку без вигоди для S-фічі.

## Decision outcome

**Chosen:** Option 1. `learning_sessions` отримує колонку kind. «Ще одне слово» створює session kind = extra_word з карткою нового слова й перевіркою; «Потренувати вивчені» — session kind = extra_practice до 20 карток. Одна активна session на learner (наявне часткове обмеження) природно повертає наявну extra-картку при повторному старті. Початок звичайного заняття закриває активну extra-session як незавершену. Запити, що рахують заняття (completed за 7 днів, нагадування, межа доби плану), фільтрують kind = plan. Картка нового слова в extra не додає слово на заміну й не змінює плановий облік ліміту; походження відповіді фіксується в exercise_kind журналу (`extra_new_word_check`, `extra_practice`).

## Consequences

**Positive**
- Повторне використання перевіреної ідемпотентності, закриття доби й екрана заняття; відповідь на картку зараховується один раз.
- Додаткові дії не змінюють completed session і нагадування за умови єдиного фільтра kind = plan.
- Походження відповіді не потребує нових колонок журналу.

**Negative / trade-offs**
- Міграція наявної таблиці: колонку kind додають у три кроки (nullable → заповнення plan → NOT NULL) без бізнес-default.
- Кожен запит про session має враховувати kind; пропущений фільтр дає тихий регрес (лічильники, нагадування) — потрібен тест регресії.
- Поведінка картки нового слова залежить від kind; ризик розповзання умов у `NewWords` (див. [SAD §11](../sad.md#11-risks-and-technical-debt)).

**Neutral**
- Нова колонка стосується лише таблиці session; журнал відповідей і стан слів не мігрують.

## Related

- [PRD unlimited-learning](../PRD.md) · [SAD](../sad.md) · [idea-brief](../idea-brief.md)
- [ADR-0002](0002-model-extra-practice-as-a-third-exercise-kind-in-the-scheduler.md) · [ADR-0008](../../personal-trainer-mvp/adr/0008-keep-the-learning-session-server-side-with-per-card-idempotency.md) · [ADR-0009](../../personal-trainer-mvp/adr/0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md)
