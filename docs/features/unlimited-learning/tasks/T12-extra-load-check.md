---
id: T12
title: "Перевірити швидкість старту extra й практики на навантажувальному наборі"
layer: "tests"
deps: ["[T09](./T09-extra-endpoints-contract.md)"]
acs: ["AC-01", "AC-08"]
files_hint: ["tests/", "ConstantLearning.Tests/Learning/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T12 — Перевірити швидкість старту extra й практики на навантажувальному наборі

**Шар:** `tests` · **Залежить від:** [T09](./T09-extra-endpoints-contract.md) · **AC:** [AC-01](../PRD.md#ac-01---happy-path), [AC-08](../PRD.md#ac-08---happy-path) · **Оцінка:** S

## Why

[PRD §6](../PRD.md#6-non-functional-requirements), [SAD §10](../sad.md#10-quality-requirements): QG-2 і QG-3.

## What

Навантажувальний сценарій: 50 одночасних learner, 1 запит на секунду, 2 хвилини; виміряти p95 старту extra-слова й запуску практики з 20 слів і занести результат у звіт.

Файли: `tests/`, `ConstantLearning.Tests/Learning/`

## Definition of Done

- [ ] p95 старту extra-слова ≤ 500 ms і запуску практики ≤ 800 ms на навантажувальному наборі
- [ ] результат записано в test-output фічі
- [ ] `dotnet build` і тести без нових попереджень
