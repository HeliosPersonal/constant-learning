---
status: Draft
owner: "Viacheslav Melnichenko"
updated_at: "2026-11-04"
---

# Tracker — multi-target-languages

Епік і граф залежностей — [_epic.md](./_epic.md). Першоджерела лінкуються в кожній задачі
(не дублюються). Контент en/de/nl ще не підготовлено — мови лишаються `is_enabled=false`
([data-model.md §Seeds](../data-model.md), OQ-5/OQ-7).

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
| [T07](./T07-generalize-forms-normalization.md) | Узагальнення пошуку форм і нормалізації (зняти хардкод `"pl"`) | domain | &lt;TBD Tech Lead&gt; | L | [T05](./T05-migrate-content-entries.md) | blocked |
| [T08](./T08-migrate-learner-words-answer-events.md) | Міграції дискримінатора `learner_words` + `answer_events` | migration | Viacheslav Melnichenko | M | [T05](./T05-migrate-content-entries.md) | todo |
| [T09](./T09-migrate-sessions-reports.md) | Міграції дискримінатора `learning_sessions` + `content_reports` | migration | Viacheslav Melnichenko | M | [T05](./T05-migrate-content-entries.md), [T08](./T08-migrate-learner-words-answer-events.md) | todo |
| [T10](./T10-progress-isolation.md) | Ізоляція обліку прогресу за `(learner, мова)` | domain | Viacheslav Melnichenko | L | [T04](./T04-per-language-day-state.md), [T08](./T08-migrate-learner-words-answer-events.md), [T09](./T09-migrate-sessions-reports.md) | todo |
| [T11](./T11-read-apis-error-registry.md) | `listTargetLanguages` + `listLearnerLanguages` + реєстр помилок | app | Viacheslav Melnichenko | M | [T01](./T01-config-target-languages.md), [T03](./T03-migrate-identity-day-state.md), [T06](./T06-base-course-availability.md) | todo |
| [T12](./T12-start-learner-language.md) | `startLearnerLanguage` (ідемпотентний старт + активація) | app | &lt;TBD Tech Lead&gt; | L | [T04](./T04-per-language-day-state.md), [T06](./T06-base-course-availability.md), [T11](./T11-read-apis-error-registry.md) | blocked |
| [T13](./T13-set-active-scope-existing.md) | `setActiveLanguage` + scoping наявних ендпоінтів | app | Viacheslav Melnichenko | L | [T10](./T10-progress-isolation.md), [T12](./T12-start-learner-language.md) | todo |
| [T14](./T14-editor-queue-by-language.md) | `listEditorQueueByLanguage` + прив'язка report до мови | app | &lt;TBD Security Lead&gt; | M | [T09](./T09-migrate-sessions-reports.md), [T11](./T11-read-apis-error-registry.md) | blocked |
| [T15](./T15-ui-language-switcher.md) | SPA: вибір/перемикання мови + порожній стан | ui | Viacheslav Melnichenko | L | [T13](./T13-set-active-scope-existing.md) | todo |
| [T16](./T16-ui-editor-queue-by-language.md) | SPA: черга content reports редактора за мовою | ui | Viacheslav Melnichenko | M | [T14](./T14-editor-queue-by-language.md) | todo |
| [T17](./T17-isolation-contract-e2e-load.md) | Наскрізна ізоляція / контракт / e2e / навантаження | tests | Viacheslav Melnichenko | L | [T02](./T02-test-fixtures.md), [T13](./T13-set-active-scope-existing.md), [T14](./T14-editor-queue-by-language.md), [T15](./T15-ui-language-switcher.md), [T16](./T16-ui-editor-queue-by-language.md) | todo |

## Заблоковані відкритим питанням

- **T07** — `blocked` на **OQ-5** (owner: Tech Lead): правила нормалізації/словоформ для de/nl. Каркас pl не блокується; de/nl лишаються неповними по AC-08 до рішення.
- **T12** — `blocked` на **OQ-4** (owner: Tech Lead): модель конкурентності старту/перемикання в різних вкладках (xmin vs retry). Контракт уже фіксує `language.concurrent_update` (409).
- **T14** — `blocked` на **OQ-9** (owner: Security Lead): чи обмежувати редактора мовами. Поки роль єдина — без per-language authz; рішення «обмежити» додає 403-гілку.

> Інші open questions ([PRD §8](../PRD.md#8-open-questions)) due before implement-tasks: OQ-6 (local equivalents), OQ-7 (per-language джерела), OQ-8 (формат транскрипції) — owner: Viacheslav Melnichenko.
