---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0012 — Search via a precomputed table of folded word forms

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

AC-30 вимагає пошуку за будь-якою граматичною формою українського перекладу чи польського слова, без діакритики, за початком від 2 символів і точним збігом з 1 символу, з порядком base course; latency p95 ≤ 300 ms (PRD §6).

## Decision drivers

- AC-30, AC-32, AC-33: лише `approved`, зникнення після зняття статусу
- Latency p95 пошуку ≤ 300 ms
- Обсяг: 2 000 лем × десятки форм
- Один екземпляр, без додаткових сховищ

## Considered options

1. **Таблиця форм у PostgreSQL** — `search_form(form_folded, form_display, lang, entry_id, version_id, is_lemma)` з B-tree `text_pattern_ops`; нормалізація й згортання в .NET однаково при записі й запиті.
2. **Повнотекстовий пошук PostgreSQL зі словниками hunspell** — Стемінг замість повного переліку форм.
3. **Зовнішній рушій** — Elasticsearch з морфологічними плагінами або Meilisearch.

## Decision outcome

**Chosen:** Option 1. Префіксний пошук у B-tree на сотнях тисяч рядків займає одиниці мілісекунд, а стемінг FTS і рушіїв не дає правил AC-30 (префікс довільної форми, точний збіг першим, порядок курсу); зовнішній рушій — зайвий контейнер для 50 learner.

## Consequences

**Positive**
- Пошук і зміна статусу узгоджені одразу, бо дані в одній БД
- Морфологічні джерела для польської: PoliMorf і Morfeusz 2 (2-clause BSD)

**Negative**
- Потрібен конвеєр генерації форм і перебудова при кожній публікації
- Для української ліцензійне рішення щодо VESUM (CC BY-NC-SA 4.0) потрібне до запуску (SAD §11)
- Одруки в запиті не знаходяться

**Neutral**
- Слово поза лексиконом індексується лише базовою формою з позначкою для редактора

## Links

- PRD: [[../PRD.md]]
- SAD: [[../sad.md]] §8
- Related ADR: [[0010-version-dictionary-entries-in-postgres-with-a-published-pointer]]
