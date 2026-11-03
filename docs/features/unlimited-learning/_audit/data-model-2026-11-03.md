# Audit — data-model unlimited-learning (2026-11-03)

## Generated files

- [data-model.md](../data-model.md)
- Staged-міграції (ще не в живому дереві; `implement-tasks` просуває їх у реальні EF-міграції зі свіжими відмітками часу):
  - `migrations/20261103120000_add_kind_to_learning_sessions.{up,down}.sql`
  - `migrations/20261103120001_backfill_kind_in_learning_sessions.{up,down}.sql`
  - `migrations/20261103120002_set_kind_not_null_on_learning_sessions.{up,down}.sql`

## Self-check

| Перевірка | Результат |
|---|---|
| Naming (plural snake_case, `<timestamp>_<verb>_<entity>`) | OK |
| down.sql відтворює up.sql (ADD COLUMN ↔ DROP COLUMN; SET NOT NULL ↔ DROP NOT NULL; backfill ↔ NULL) | OK |
| Індекси на FK | не застосовується: нових FK немає |
| Заборонене (CHECK, TRIGGER, business DEFAULT) | 0 знахідок |

## Convention divergences

- Живе дерево використовує EF-міграції (`ConstantLearning/Migrations/*.cs`), а course default — SQL-first. Staged-SQL лишається контрактом, `implement-tasks` розкладає його в EF-міграції; так само вчинила батьківська фіча.
- Один рядок backfill містить літерал `plan` у UPDATE: це дані, а не DEFAULT колонки.

## Drift

- **Schema vs source:** `LearningSession` у коді ще не має властивості `Kind` — очікувано до реалізації; fix-міграцій у `_drift/` немає, бо розбіжність закриває сама фіча (T-задача з додаванням властивості).
- **Model vs spec:** усі сутності PRD покрито: extra new word і extra practice → `learning_sessions.kind`; походження відповіді → `answer_events.exercise_kind`; сирітських таблиць немає.

## Breaking changes decomposed

- Новий NOT NULL на наявній таблиці `learning_sessions` → expand → backfill → contract (три кроки вище).

## TBD

- Немає.
