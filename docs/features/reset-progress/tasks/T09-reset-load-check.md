---
id: T09
title: "Перевірити скидання на повному курсі й навантажувальному наборі"
layer: "tests"
deps: ["[T04](./T04-reset-endpoint.md)"]
acs: []
files_hint: ["tests/load/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T09 — Перевірити скидання на повному курсі й навантажувальному наборі

**Шар:** `tests` · **Залежить від:** [T04](./T04-reset-endpoint.md) · **AC:** — · **Оцінка:** S

## Why

[PRD §6](../PRD.md#6-non-functional-requirements): p95 ≤ 2 000 ms для 2 000 слів і 20 000 відповідей.

## What

Сценарій 50 послідовних скидань на повному наборі даних у наявному інструменті навантажувальних тестів проєкту.

Файли: `tests/load/`

## Definition of Done

- [ ] p95 часу скидання ≤ 2 000 ms зафіксовано в звіті
- [ ] `dotnet build`, тести й лінтер без нових попереджень
