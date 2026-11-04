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

- [x] p95 часу скидання ≤ 2 000 ms зафіксовано в звіті ([reset-benchmark.md](../test-output/reset-benchmark.md): 1 866.7 ms)
- [x] `dotnet build`, тести й лінтер без нових попереджень

## Статус

Артефакти готові: сценарій [`reset.js`](../../../../tests/load/reset.js), seed-когорта `930000001..930000050`
(2 000 слів / 20 000 відповідей) у [`LoadHost Program.cs`](../../../../tests/load/Program.cs) та інструкція
прогону в [`tests/load/README.md`](../../../../tests/load/README.md). `LoadHost.csproj` зібрано без попереджень
(вирівняно версію `Microsoft.EntityFrameworkCore.Relational` під транзитивну з основного проєкту).

Прогін на локальному стенді (Docker + k6 0.57.0) виконано: 50 скидань, 0 помилок, p95 клієнта 1 866.7 ms
при межі 2 000 ms. Звіт — [reset-benchmark.md](../test-output/reset-benchmark.md). Серверна гістограма T08
має bucket `2500`, тому серверне p95 не підтверджує межу 2 000 ms точно; це окремий можливий крок.
