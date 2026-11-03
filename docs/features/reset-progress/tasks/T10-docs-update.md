---
id: T10
title: "Оновити README, архітектурну карту й документацію батьківської фічі"
layer: "docs"
deps: ["[T05](./T05-ui-reset-progress.md)", "[T06](./T06-reset-integration-tests.md)", "[T08](./T08-reset-telemetry-counter.md)"]
acs: []
files_hint: ["README.md", "docs/architecture-map.md", "docs/features/personal-trainer-mvp/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T10 — Оновити README, архітектурну карту й документацію батьківської фічі

**Шар:** `docs` · **Залежить від:** [T05](./T05-ui-reset-progress.md), [T06](./T06-reset-integration-tests.md), [T08](./T08-reset-telemetry-counter.md) · **AC:** — · **Оцінка:** S

## Why

Правила документації в [AGENTS.md](../../../../AGENTS.md).

## What

Додати скидання прогресу до опису можливостей у README, модуль `Identity/ProgressReset` до архітектурної карти, посилання на [reset-progress](../PRD.md) у PRD та SAD батьківської фічі; запустити перевірку посилань.

Файли: `README.md`, `docs/architecture-map.md`, `docs/features/personal-trainer-mvp/`

## Definition of Done

- [ ] `python tools/docs/check_links.py` без проблем; опис відповідає реалізації
- [ ] `dotnet build`, тести й лінтер без нових попереджень
