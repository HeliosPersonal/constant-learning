---
id: T01
title: "Додати ендпоінти /stats/* у контракт батьківської фічі й зареєструвати коди помилок stats.*"
layer: "ports"
deps: []
acs: ["AC-04", "AC-06", "AC-13"]
files_hint: ["docs/features/personal-trainer-mvp/contracts/openapi.yaml", "docs/features/learned-words-stats/contracts/openapi.yaml", "ConstantLearning/Features/Learning/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "done"
---

# T01 — Додати ендпоінти /stats/* у контракт батьківської фічі й зареєструвати коди помилок stats.*

**Шар:** `ports` · **Залежить від:** — · **AC:** [AC-04](../PRD.md#ac-04---domain-invariant-вкладки-й-зведення-узгоджені-зі-сьогодні), [AC-06](../PRD.md#ac-06---error-картка-приховує-непоказані-слова), [AC-13](../PRD.md#ac-13---error-невалідний-запит-і-неонбордений-learner) · **Оцінка:** S

## Why

[openapi](../contracts/openapi.yaml) (x-amendments), [idea-brief §13](../idea-brief.md#13-recommendation): лічильники читаються наживо з таблиць, які очищає reset-progress, без нових таблиць.

## What

Додати в контракт personal-trainer-mvp три нові шляхи `GET /api/v1/stats/words` (listLearnedWords), `GET /api/v1/stats/words/{entry_id}` (getLearnedWordCard) і `GET /api/v1/stats/summary` (getLearnedWordsSummary) з новими схемами (StatsTab, StatsSort, MistakeBreakdown, LearnedWordStat, LearnedWordCard, LearnedWordsPage, LearnedWordsSummary) без зміни наявних схем. Підняти батьківський `info.version` до 0.4.0 рядком у CHANGELOG у тому самому PR. Зареєструвати коди помилок `stats.invalid_request` і `stats.word_not_found` у реєстрі помилок Learning з тими самими рядками, що в контракті. Перезапустити синхронізацію контракту фічі.

Файли: `docs/features/personal-trainer-mvp/contracts/openapi.yaml`, `docs/features/learned-words-stats/contracts/openapi.yaml`, `ConstantLearning/Features/Learning/`

## Definition of Done

- [ ] api-sync-report після повторного запуску має Verdict PASS або PASS (with deferrals)
- [ ] кожен код помилки з контракту має одне визначення в реєстрі з тим самим рядком
- [ ] `info.version` 0.4.0 має рядок у CHANGELOG, жодна наявна схема не змінена
- [ ] `spectral lint` обох контрактів без помилок
