---
id: T33
title: "Приймати клієнтську телеметрію й публікувати серверні метрики"
layer: "ports"
deps: ["T01"]
acs: []
files_hint: ["ConstantLearning/Features/Learning/Telemetry/", "ConstantLearning/Platform/Metrics/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T33 — Приймати клієнтську телеметрію й публікувати серверні метрики

**Шар:** `ports` · **Залежить від:** [T01](./T01-module-skeleton.md) · **AC:** — (технічна передумова) · **Оцінка:** S · **Фаза:** 2

## Why

NFR «Сьогодні» з [PRD §6](../PRD.md); [sad §7](../sad.md) Monitoring; `reportClientTiming`.

## What

Операція `reportClientTiming` і серверні гістограми latency оцінки відповіді й пошуку.

Файли: `ConstantLearning/Features/Learning/Telemetry/`, `ConstantLearning/Platform/Metrics/`

## Definition of Done

- [ ] невідома метрика повертає `telemetry.invalid_metric`
- [ ] метрики видно на локальному endpoint метрик
- [ ] `dotnet build`, тести й лінтер без нових попереджень
