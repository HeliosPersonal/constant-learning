---
id: T13
title: "Оновити README, CONTEXT і документацію батьківської фічі"
layer: "docs"
deps: ["[T10](./T10-ui-today-and-session.md)", "[T11](./T11-extra-integration-tests.md)"]
acs: ["AC-16"]
files_hint: ["README.md", "CONTEXT.md", "docs/features/personal-trainer-mvp/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T13 — Оновити README, CONTEXT і документацію батьківської фічі

**Шар:** `docs` · **Залежить від:** [T10](./T10-ui-today-and-session.md), [T11](./T11-extra-integration-tests.md) · **AC:** [AC-16](../PRD.md#ac-16---happy-path) · **Оцінка:** S

## Why

[AGENTS.md](../../../../AGENTS.md): документація українською, посилання між документами.

## What

Описати в README дії «Ще одне слово» і «Потренувати вивчені»; додати в CONTEXT інваріанти extra (ліміт лишається планом, одна практика на слово за добу, походження відповіді); у документації personal-trainer-mvp додати примітку про розширення правил «Вже пригадую» (AC-18, AC-19 базового PRD) і запустити `python tools/docs/check_links.py`.

Файли: `README.md`, `CONTEXT.md`, `docs/features/personal-trainer-mvp/`

## Definition of Done

- [ ] README і CONTEXT описують фічу
- [ ] перевірка посилань без проблем
- [ ] `dotnet build` і тести без нових попереджень
