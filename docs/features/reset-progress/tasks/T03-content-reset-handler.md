---
id: T03
title: "Додати обробник скидання в Content: розірвати привʼязку reports до відповідей"
layer: "app"
deps: ["[T01](./T01-reset-flow-and-port.md)"]
acs: ["AC-10"]
files_hint: ["ConstantLearning/Features/Content/Reports/", "ConstantLearning.Tests/Content/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T03 — Додати обробник скидання в Content: розірвати привʼязку reports до відповідей

**Шар:** `app` · **Залежить від:** [T01](./T01-reset-flow-and-port.md) · **AC:** AC-10 · **Оцінка:** S

## Why

Content reports мають зовнішній ключ на відповіді без каскаду ([data-model](../../personal-trainer-mvp/data-model.md)); вимога [PRD AC-10](../PRD.md#5-acceptance-criteria).

## What

Обробник очищає посилання `answer_event` у content reports learner до видалення відповідей і не змінює самих reports, їхніх псевдонімів і статусів. Виконується першим, до обробника Learning. Реєструється в `ContentReportsModule`.

Файли: `ConstantLearning/Features/Content/Reports/`, `ConstantLearning.Tests/Content/`

## Definition of Done

- [ ] content report після скидання лишається видимим content editor без привʼязки до відповіді (AC-10)
- [ ] `dotnet build`, тести й лінтер без нових попереджень
