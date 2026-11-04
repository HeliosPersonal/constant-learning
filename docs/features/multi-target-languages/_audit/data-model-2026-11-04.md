# Audit — generate-data-model (multi-target-languages)

- **Stage:** 07 (`sdlc:generate-data-model`, `--mode brownfield`)
- **Date:** 2026-11-04
- **Feature size:** M
- **Base schema:** остання застосована EF-міграція `20261103185149`; стейджені файли фічі `20261104hhmmss`.

## Generated files

- `data-model.md` — brownfield-дельта: ER, 2 нові сутності (`target_languages`, `learner_language_states`), 6 ALTER-таблиць, таблиця індексів із обґрунтуванням, план міграцій, seeds, фікстури, TBD.
- `migrations/` — **29 пар** `.up.sql` + `.down.sql` (58 файлів), позначки `20261104090000`…`20261104092800`. Повний перелік — у `data-model.md` §«Міграційний план».

## Design decisions (grounded in approved ADR + SAD §8)

| Decision | Джерело |
|---|---|
| Один learner + `active_target_language` + `target_language`-дискримінатор на таблицях прогресу | ADR-0001; backfill `'pl'` |
| Повний per-language стан доби у новій `learner_language_states` (PK `learner_id, target_language`) | ADR-0002 (свідомо важчий вибір за PRD OQ-1 default) |
| `target_language` на `dictionary_entries`; `external_id` та `course_position` — per-language | ADR-0003 |
| `target_languages` конфіг-таблиця (не enum у коді); `code` VARCHAR(2) як природний PK | ADR-0004 |
| Денормалізація `target_language` на `learner_words`/`answer_events`/`learning_sessions`/`content_reports` | ключ ізоляції SAD §8 `(learner_id, target_language)`; quality goal №3 (гарячі лічильники без join) |

## Default deviations applied

- **PK `target_languages.code` (VARCHAR(2))** замість UUID v7 — природний ключ-дискримінатор, яким посилаються всі таблиці прогресу/контенту; UUID-сурогат змусив би денормалізувати `code` окремо. Прецедент природного ключа — `reminder_schedules(learner_id)` PK.
- **Складений PK `learner_language_states(learner_id, target_language)`** замість UUID-сурогату — enrollment/join-таблиця, ключована двома FK.
- **Колапс add+backfill в одному файлі** (замість 3 окремих), `SET NOT NULL` — окремим файлом: backfill константний/`entry_id`-похідний на pre-pilot даних, без батчингу. 3-крок зеро-даунтайм збережено (nullable → backfill → NOT NULL після деплою коду, що пише колонку).

## Breaking changes decomposed

- **Перенесення стану доби з `learners` у `learner_language_states`** — фаза expand+backfill (ця фіча, dual-write у коді). Фаза **contract** (DROP старих колонок `learners.current_day_*` тощо) — окремий PR/deploy після переходу застосунку. Зафіксовано в `data-model.md` §«Борг».
- **Своп унікальності `external_id`** (глобальний → `(target_language, external_id)`) і **партіального `course_position`** — розбито: спершу `CREATE … CONCURRENTLY` нового індексу, потім `DROP` старого (без вікна без індексу).
- **Старі не-мово-свідомі індекси** (`ix_learner_words_learner_due/status`, `ix_answer_events_learner_day_outcome`) лишені additively; чистка — contract-фаза.

## Self-check (4 mandatory) — PASS

- **Naming:** PASS. plural snake_case таблиці (`target_languages`, `learner_language_states`); snake_case колонки; індекси `ix_*`/`ux_*`.
- **down reversibility:** PASS. 29/29 пар; кожен `.up.sql` має парний `.down.sql` зі зворотною дією (ADD↔DROP COLUMN, CREATE↔DROP INDEX, SET↔DROP NOT NULL, INSERT↔DELETE, своп індексів реверсується). Жодного осиротілого up/down.
- **FK indexes:** PASS. 7 таблиць із `REFERENCES target_languages (code)`, у кожної індекс на FK-колонці: `ix_learner_language_states_target_language`, `ix_learners_active_target_language`, `ix_learner_words_target_language`, `ix_answer_events_target_language`, `ix_learning_sessions_target_language`, `ix_content_reports_target_language`; `dictionary_entries.target_language` покрито провідною колонкою `ux_dictionary_entries_lang_external_id`/`ix_dictionary_entries_lang_course_position`. `learner_language_states.learner_id` FK — провідна колонка складеного PK.
- **Forbidden features:** PASS. немає `CHECK (`, `CREATE TRIGGER`, бізнес-`DEFAULT '…'` (лише `DEFAULT now()`), немає stored procs/functions.
- **CONCURRENTLY:** 12 up-файлів із `CREATE INDEX CONCURRENTLY`, кожен — рівно один statement (tx-wrapper vs CONCURRENTLY).

## TBDs

- `target_languages.normalization_profile` для `de`/`nl` — **OQ-5** (Tech Lead): NULL до підтвердження каркасу форм/нормалізації; AC-08 для мови неповна.
- Формат підказки вимови на мову (`pronunciation_uk`) — **OQ-8** (Viacheslav): можлива окрема per-language колонка транскрипції.
- Перейменування `entry_versions.lemma_pl`/`example_pl` → мово-нейтральні імена — борг SAD §11 (3-крок, окремий PR).

## Next stage

`sdlc:api-forge multi-target-languages` — OpenAPI 3.1.0 контракт (`contracts/openapi.yaml`) за `data-model.md` + SAD §6 + PRD AC.
