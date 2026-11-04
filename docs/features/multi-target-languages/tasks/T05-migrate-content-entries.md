---
id: T05
title: "Міграції dictionary_entries.target_language + своп індексів"
layer: "migration"
deps: ["[T01](./T01-config-target-languages.md)"]
acs: ["AC-06", "AC-08"]
files_hint:
  - "ConstantLearning/Migrations/"
  - "ConstantLearning/Features/Content/Data/"
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T05 — Міграції `dictionary_entries.target_language` + своп індексів

**Шар:** migration · **Залежить від:** [T01](./T01-config-target-languages.md) · **AC:** AC-06, AC-08 · **Оцінка:** M · **Фаза:** 3 (Content)

## Why

[ADR-0003](../adr/0003-place-target-language-on-dictionary-entry.md): один запис словника належить
конкретній мові, `external_id` унікальний **у межах мови**, а `course_position` — per-language
впорядкування. Це передумова власного base course на мову (AC-06) і мово-свідомого контенту (AC-08).

## What

Перенести стейджені пари (кроки 7–12, [data-model.md §Міграційний план](../data-model.md)):

- `alter_dictionary_entries_add_target_language` — ADD nullable + inline FK + backfill `'pl'`.
- `dictionary_entries_target_language_not_null` — `SET NOT NULL` (крок 3 з 3).
- `create_dictionary_entries_lang_external_id_unique` — `CREATE UNIQUE INDEX CONCURRENTLY` (target_language, external_id).
- `drop_dictionary_entries_external_id_unique` — DROP глобального unique (після створення нового — без вікна без індексу).
- `create_dictionary_entries_lang_course_position_index` — `CREATE INDEX CONCURRENTLY` partial `WHERE published_version_id IS NOT NULL`.
- `drop_dictionary_entries_course_position_index` — DROP старого.
- EF-сутність: `DictionaryEntry.TargetLanguage` + мапінг.

## Definition of Done

- [ ] Усі 6 кроків застосовано на testcontainer у вказаному порядку; кожен `CONCURRENTLY` — один statement (`suppressTransaction: true`).
- [ ] Своп індексів не лишає вікна без унікальності/впорядкування; `.down` відновлює глобальні індекси.
- [ ] Backfill → `'pl'` усім наявним записам; `SET NOT NULL` проходить.
- [ ] FK `target_language` покрито провідною колонкою unique/partial індексів (self-check FK зелений).
- [ ] PR ≤500 LOC.
