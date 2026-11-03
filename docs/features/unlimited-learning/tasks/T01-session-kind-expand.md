---
id: T01
title: "Додати колонку kind у learning_sessions і писати її в нових session (expand)"
layer: "migration"
deps: []
acs: ["AC-18"]
files_hint: ["docs/features/unlimited-learning/migrations/20261103120000_add_kind_to_learning_sessions.up.sql", "ConstantLearning/Migrations/", "ConstantLearning/Features/Learning/Data/LearningSession.cs", "ConstantLearning/Features/Learning/Data/LearningSessionConfiguration.cs", "ConstantLearning/Features/Learning/Sessions/Start/StartSessionService.cs"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T01 — Додати колонку kind у learning_sessions і писати її в нових session (expand)

**Шар:** `migration` · **Залежить від:** — · **AC:** [AC-18](../PRD.md#ac-18---domain-invariant) · **Оцінка:** S

## Why

[ADR-0001](../adr/0001-run-extras-as-typed-sessions-over-the-existing-session-pipeline.md), [data-model](../data-model.md): крок 1 з 3 (expand) розкладеного NOT NULL.

## What

Просунути staged-міграцію кроку 1 у EF-міграцію: колонка `kind` VARCHAR(16) nullable без default. Додати `Kind` у `LearningSession` і писати `plan` у `StartSessionService` для нових session. Значення `plan` / `extra_word` / `extra_practice` — константи в застосунку.

Файли: `docs/features/unlimited-learning/migrations/20261103120000_add_kind_to_learning_sessions.up.sql`, `ConstantLearning/Migrations/`, `ConstantLearning/Features/Learning/Data/LearningSession.cs`, `ConstantLearning/Features/Learning/Data/LearningSessionConfiguration.cs`, `ConstantLearning/Features/Learning/Sessions/Start/StartSessionService.cs`

## Definition of Done

- [ ] міграція застосовується й відкочується без втрати даних (інтеграційний тест на тимчасовій БД)
- [ ] нова планова session має `kind = plan`
- [ ] наявні тести session зелені
- [ ] `dotnet build` і тести без нових попереджень
