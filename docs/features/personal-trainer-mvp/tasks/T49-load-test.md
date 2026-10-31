---
id: T49
title: "Перевірити бюджети латентності під навантаженням"
layer: "tests"
deps: ["T45", "T17", "T19", "T22", "T33"]
acs: []
files_hint: ["tests/load/", ".github/workflows/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T49 — Перевірити бюджети латентності під навантаженням

**Шар:** `tests` · **Залежить від:** [T45](./T45-deployment-fixes.md), [T17](./T17-answer-item.md), [T19](./T19-today-api.md), [T22](./T22-dictionary-search-api.md), [T33](./T33-client-telemetry.md) · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 9

## Why

NFR латентності з [PRD §6](../PRD.md) і QG-2 у [sad §10](../sad.md#10-quality-requirements).

## What

Сценарій навантаження на 100 одночасних learner: відкриття «Сьогодні», відповіді на картки, пошук; звіт p95 за серверними метриками з T33.

Файли: `tests/load/`, `.github/workflows/`

## Definition of Done

- [ ] на 100 одночасних learner p95 оцінки ≤ 500 ms і пошуку ≤ 300 ms
- [ ] звіт прогону збережено як артефакт CI
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

Запускається вручну перед пілотом, не на кожен PR.
