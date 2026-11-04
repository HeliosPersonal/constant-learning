# Sequence coverage audit — multi-target-languages

**Date:** 2026-11-04
**Stage:** 06 (complete-sequence-diagrams)
**Source:** PRD §4 (US-01…US-07) × SAD §6 Runtime view

## Coverage before this pass

| US | Title | Status | Covered by |
|---|---|---|---|
| US-01 | Обрати мову для вивчення | Covered | flow 1 (happy-start, folded), flow 3 (AC-02 no-content), flow 4 (AC-12 idempotent) |
| US-02 | Перемкнути активну мову | Covered | flow 1 (AC-03) |
| US-03 | Вести окремий прогрес на кожну мову | Covered | flow 2 (AC-05/AC-06); AC-04/AC-10 as §8 crosscutting rules |
| US-04 | Почати нову мову з порожнього стану | Covered | flow 1 ("word status лише EN, без лічильників інших мов" = AC-07) |
| US-05 | Вчити слово з вимовою й формами активної мови | **Missing** | — |
| US-06 | Повідомити про неточність у активній мові | **Missing** | — |
| US-07 | Готувати й схвалювати контент кожної мови окремо | **Missing** | — |

No user story was classified trivial — each of US-05/06/07 carries a domain-invariant or cross-context AC (AC-08/AC-09/AC-11).

## Added (this pass)

- **Critical flow 5 — US-05 (AC-08):** вправа активної мови; Content читає `entry_versions` + `search_forms WHERE target_language`, Learning нормалізує відповідь за правилами мови, пошук за формами `WHERE Lang=EN`. `alt`-гілка на випадок непідтвердженого каркасу нормалізації/форм (OQ-5) → часткова AC-08. Latency-note на §10 (оцінка ≤ 500 ms, пошук ≤ 300 ms).
- **Critical flow 6 — US-06 (AC-09):** content report прив'язується до `entry` активної мови й лягає в чергу цієї мови; `alt` на добовий ліміт повідомлень learner (reference AC-46). Контент інших мов без змін.
- **Critical flow 7 — US-07 (AC-11):** редактор відкриває чергу з розмежуванням `GROUP BY target_language`; схвалення/відкат застосовується лише до `dictionary_entries WHERE target_language` мови повідомлення; `alt` на коректну мовну маршрутизацію.

## Skipped (trivial)

- None.

## New actors flagged (for §5 Container view)

- **Content editor** — у flow 7 як `actor editor`. §5 C4Container містить лише системні контейнери (`spa`, `identity`, `content`, `learning`, `db`); редактор — людський stakeholder із §1/§3, що взаємодіє через `spa`. §5 не виділяє окрему редакторську поверхню. Рішення про окрему editor-поверхню не входить у цю фічу; за потреби — окремий прохід по §5.

## ADR potential (flagged, not auto-generated)

- **US-05 / OQ-5:** per-language каркас нормалізації відповіді й генерації словоформ (DE/NL рід, капіталізація, складені слова). Вже відкрите питання §11 (owner: Tech Lead, due before implement-tasks). Коли OQ-5 розв'яжеться — кандидат на ADR стратегії per-language нормалізації/форм.
- **US-07 / OQ-9:** чи обмежувати редактора мовами, які він здатен верифікувати. Вже відкрите питання §11 (owner: Security Lead). Розв'язання може стати ADR про мовне скоупування ролі редактора.
- **US-06:** повторне використання reference-патерну добового ліміту повідомлень — нового ADR не потребує.

## Validation

- **mmdc `--parse-only`: NOT RUN** — Node/npm/npx toolchain недоступний у цьому середовищі (`command -v node npm npx` порожній; кореневого `package.json` немає). Три нові блоки написані дзеркально до чотирьох уже наявних у §6 валідних блоків: ті самі конструкції (`actor`, `participant … as …`, `->>`, `-->>`, `alt`/`else`/`end`, `Note over A, B:`); усі учасники оголошені перед використанням; усі `alt`-блоки збалансовані `end`. **Дію:** прогнати `mmdc --parse-only` на §6 у середовищі з Node перед підписанням stage 06.

## Self-check (DoD)

- Кожна PRD US — Covered або має свіжий Mermaid-блок у §6: 7/7 ✓
- Нові блоки структурно валідні (ручна звірка; mmdc відкладено — див. вище) ✓
- Аудит-звіт збережено ✓

## Next stage

`sdlc:generate-data-model multi-target-languages` (stage 07 — Backend Lead). Нові flow-и 5–7 дають index-кандидати: `search_forms(Lang)`, `content_reports(target_language)`, `dictionary_entries(target_language, CoursePosition)`.
