---
status: Draft
owner: "Viacheslav Melnichenko"
updated_at: "2026-11-04"
---

# Tracker — multi-target-languages

Епік і граф залежностей — [_epic.md](./_epic.md). Першоджерела лінкуються в кожній задачі
(не дублюються). Контент en/de/nl ще не підготовлено — мови лишаються `is_enabled=false`
до наявності контенту ([data-model.md §Seeds](../data-model.md), OQ-7); каркас нормалізації/форм
для них кодується одразу (OQ-5, профілі `de`/`nl`).

> Статус кожної задачі епіку. `implement-tasks` оновлює `done`, коли комітить задачу.
> Стани: `todo` · `in_progress` · `blocked` · `review` · `done`. Оцінка: S = 2 год, M = пів дня, L = день.

| # | Task | Layer | Owner | Estimate | Blocked by | Status |
|---|---|---|---|---|---|---|
| [T01](./T01-config-target-languages.md) | Конфіг-таблиця `target_languages` + seed + сутність/репозиторій | migration+infra | Viacheslav Melnichenko | M | — | todo |
| [T02](./T02-test-fixtures.md) | Per-language фікстури й оснастка ізоляційних тестів | tests | Viacheslav Melnichenko | M | [T01](./T01-config-target-languages.md) | todo |
| [T03](./T03-migrate-identity-day-state.md) | Міграції `learner_language_states` + `learners.active_target_language` | migration | Viacheslav Melnichenko | M | [T01](./T01-config-target-languages.md) | todo |
| [T04](./T04-per-language-day-state.md) | Per-language стан доби learner (`ILearnerDayStateService`) | domain | Viacheslav Melnichenko | L | [T03](./T03-migrate-identity-day-state.md) | todo |
| [T05](./T05-migrate-content-entries.md) | Міграції `dictionary_entries.target_language` + своп індексів | migration | Viacheslav Melnichenko | M | [T01](./T01-config-target-languages.md) | todo |
| [T06](./T06-base-course-availability.md) | Base course на мову + перевірка доступності мови | domain | Viacheslav Melnichenko | M | [T01](./T01-config-target-languages.md), [T05](./T05-migrate-content-entries.md) | todo |
| [T07](./T07-generalize-forms-normalization.md) | Узагальнення пошуку форм і нормалізації (зняти хардкод `"pl"`) + правила de/nl | domain | Viacheslav Melnichenko | L | [T05](./T05-migrate-content-entries.md) | todo |
| [T08](./T08-migrate-learner-words-answer-events.md) | Міграції дискримінатора `learner_words` + `answer_events` | migration | Viacheslav Melnichenko | M | [T05](./T05-migrate-content-entries.md) | todo |
| [T09](./T09-migrate-sessions-reports.md) | Міграції дискримінатора `learning_sessions` + `content_reports` | migration | Viacheslav Melnichenko | M | [T05](./T05-migrate-content-entries.md), [T08](./T08-migrate-learner-words-answer-events.md) | todo |
| [T10](./T10-progress-isolation.md) | Ізоляція обліку прогресу за `(learner, мова)` | domain | Viacheslav Melnichenko | L | [T04](./T04-per-language-day-state.md), [T08](./T08-migrate-learner-words-answer-events.md), [T09](./T09-migrate-sessions-reports.md) | todo |
| [T11](./T11-read-apis-error-registry.md) | `listTargetLanguages` + `listLearnerLanguages` + реєстр помилок | app | Viacheslav Melnichenko | M | [T01](./T01-config-target-languages.md), [T03](./T03-migrate-identity-day-state.md), [T06](./T06-base-course-availability.md) | todo |
| [T12](./T12-start-learner-language.md) | `startLearnerLanguage` (ідемпотентний старт + активація) | app | Viacheslav Melnichenko | L | [T04](./T04-per-language-day-state.md), [T06](./T06-base-course-availability.md), [T11](./T11-read-apis-error-registry.md) | todo |
| [T13](./T13-set-active-scope-existing.md) | `setActiveLanguage` + scoping наявних ендпоінтів | app | Viacheslav Melnichenko | L | [T10](./T10-progress-isolation.md), [T12](./T12-start-learner-language.md) | todo |
| [T14](./T14-editor-queue-by-language.md) | `listEditorQueueByLanguage` + прив'язка report до мови | app | Viacheslav Melnichenko | M | [T09](./T09-migrate-sessions-reports.md), [T11](./T11-read-apis-error-registry.md) | todo |
| [T15](./T15-ui-language-switcher.md) | SPA: вибір/перемикання мови + порожній стан | ui | Viacheslav Melnichenko | L | [T13](./T13-set-active-scope-existing.md) | todo |
| [T16](./T16-ui-editor-queue-by-language.md) | SPA: черга content reports редактора за мовою | ui | Viacheslav Melnichenko | M | [T14](./T14-editor-queue-by-language.md) | todo |
| [T17](./T17-isolation-contract-e2e-load.md) | Наскрізна ізоляція / контракт / e2e / навантаження | tests | Viacheslav Melnichenko | L | [T02](./T02-test-fixtures.md), [T13](./T13-set-active-scope-existing.md), [T14](./T14-editor-queue-by-language.md), [T15](./T15-ui-language-switcher.md), [T16](./T16-ui-editor-queue-by-language.md) | todo |

## Відкриті питання — закрито (2026-11-04), крім OQ-7

Усі OQ, крім OQ-7, закрито в інтерв'ю ([PRD §8](../PRD.md#8-open-questions)); T07/T12/T14 розблоковано.
**OQ-7** — відкрите: spike джерел на старті T07, рішення ще не зафіксовано ([SAD §11](../sad.md), [_epic.md](./_epic.md)).

- **T07** — **OQ-5** = повні правила de/nl одразу; **OQ-7** (відкрите) = spike джерел першим кроком T07. Профілі `de`/`nl` не NULL; задача розширюється (spike + повні правила) — якщо PR >500 LOC, розбити на T07a (узагальнення pl/en) + T07b (правила de/nl).
- **T12** — **OQ-4** = оптимістична `xmin` `(learner_id, target_language)`; конфлікт → `409 language.concurrent_update`, клієнт повторює. Без server-side retry.
- **T14** — **OQ-9** = єдина роль редактора для всіх мов; без per-language authz й без 403-гілки.

> Решта рішень: **OQ-1а** незалежний per-language ліміт, **OQ-2** перемикання/додавання в налаштуваннях (T15), **OQ-6** local equivalents опційно на мову, **OQ-8** єдина наближена транскрипція для всіх мов.
