---
id: T06
title: "Оновити README, CONTEXT і документацію батьківської фічі"
layer: "docs"
deps: ["[T04](./T04-ui-my-words-screen.md)", "[T05](./T05-stats-integration-tests.md)"]
acs: ["AC-05"]
files_hint: ["README.md", "CONTEXT.md", "docs/features/personal-trainer-mvp/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "done"
---

# T06 — Оновити README, CONTEXT і документацію батьківської фічі

**Шар:** `docs` · **Залежить від:** [T04](./T04-ui-my-words-screen.md), [T05](./T05-stats-integration-tests.md) · **AC:** [AC-05](../PRD.md#ac-05---happy-path-тренування-слабких-слів) · **Оцінка:** S

## Why

[AGENTS.md](../../../../AGENTS.md): документація українською, посилання між документами.

## What

Описати в README екран «Мої слова» (вкладки, лічильники, картка слова) і тренування слабких слів через наявний `POST /api/v1/sessions/extra-practice`; додати в CONTEXT інваріанти статистики (лічильники читаються наживо з журналу, оскаржений одрук не рахується, вкладка «Усі» — лише побачені слова, показ з опублікованої версії); у документації personal-trainer-mvp додати примітку про нові ендпоінти `/api/v1/stats/*` і запустити `python tools/docs/check_links.py`.

Файли: `README.md`, `CONTEXT.md`, `docs/features/personal-trainer-mvp/`

## Definition of Done

- [ ] README і CONTEXT описують екран «Мої слова» й тренування слабких слів
- [ ] перевірка посилань без проблем
- [ ] `dotnet build` і тести без нових попереджень
