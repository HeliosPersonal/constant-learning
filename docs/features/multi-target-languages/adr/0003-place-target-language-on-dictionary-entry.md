---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-11-04"
feature_size: M
stage: "04-05"
ticket: "TBD"
---

# 0003 — Розмістити target_language на dictionary_entries з порядком курсу на мову

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** Viacheslav Melnichenko (Tech Lead), архітектурна сесія

## Context

Контент сьогодні польсько-орієнтований: `dictionary_entries.CoursePosition` — єдине глобальне впорядкування, `external_id` унікальний глобально (одне слово = один запис), а `entry_versions` тримає польські колонки `LemmaPl`/`ExamplePl` + українські `PronunciationUk`/`ExampleUk` (brownfield-скан, `data-model.md`). Фіча вимагає власний base course зі своїм порядком на кожну мову (PRD AC-06). Питання: на якому рівні ввести мовний вимір контенту — на записі словника чи на його версії.

## Decision drivers

- AC-06 — тренер бере леми лише з base course активної мови в її власному порядку (PRD §5).
- Quality goal №2 — розширюваність: нова мова додає свій контент, не чіпаючи інші (sad §1).
- Наявні обмеження: глобальні `external_id` (unique) і `CoursePosition` single-track (brownfield-скан).
- Конвенція міграцій: зеро-даунтайм, бекфіл наявних рядків `'pl'` (sad §2).

## Considered options

1. **`target_language` на `dictionary_entries`** — один запис належить конкретній мові; `CoursePosition` та `external_id` стають унікальними/впорядкованими в межах мови.
2. **`target_language` на `entry_versions`** — запис-слово спільний, версії різних мов під ним.

## Decision outcome

**Chosen:** Option 1. AC-06 вимагає окремий base course із власним порядком на мову, що природно лягає на мовний вимір самого запису: `CoursePosition` → per-language впорядкування, `external_id` → унікальність у межах мови. Option 2 конфліктувала б із семантикою глобального `external_id`/`CoursePosition` і змішувала б непов'язані слова різних мов під одним записом.

## Consequences

**Positive**
- Base course кожної мови — природний зріз `WHERE target_language = ?` із власним `CoursePosition` (AC-06).
- Завершення курсу однієї мови не впливає на добір слів іншої (AC-06).
- Польські колонки `entry_versions` лишаються як є для `'pl'`; нові мови додають власні рядки, не ламаючи наявні.

**Negative**
- Унікальні індекси (`external_id`, `CoursePosition`) переводяться на складені з `target_language` — міграція наявних індексів.
- Польсько-іменовані колонки версії (`LemmaPl`/`ExamplePl`) стають семантично «лема/приклад активної мови» — назви не перейменовуються в цій фічі (борг, sad §11).

**Neutral**
- Українські `PronunciationUk`/`ExampleUk` лишаються source-мовними; формат транскрипції на мову — окреме відкрите питання (OQ-8).
- Мово-свідомий субстрат пошуку (`search_forms.Lang`) уже сумісний — див. [0004](0004-model-supported-languages-as-config-table.md).

## Links

- PRD: [PRD.md](../PRD.md) (US-05/US-07, AC-06, AC-08)
- SAD: [sad.md](../sad.md) §4, §5, §11
- Related ADR: [0001](0001-use-single-learner-with-target-language-column.md), [0004](0004-model-supported-languages-as-config-table.md)
