---
id: T11
title: "Дописати інтеграційні тести крайових випадків і відтворення журналу"
layer: "tests"
deps: ["[T08](./T08-answer-pipeline-extra.md)", "[T09](./T09-extra-endpoints-contract.md)"]
acs: ["AC-02", "AC-05", "AC-07", "AC-09", "AC-11", "AC-18", "AC-19", "AC-20"]
files_hint: ["ConstantLearning.Tests/Learning/", "ConstantLearning.Tests/Learning/Sessions/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T11 — Дописати інтеграційні тести крайових випадків і відтворення журналу

**Шар:** `tests` · **Залежить від:** [T08](./T08-answer-pipeline-extra.md), [T09](./T09-extra-endpoints-contract.md) · **AC:** [AC-02](../PRD.md#ac-02---domain-invariant), [AC-05](../PRD.md#ac-05---domain-invariant), [AC-07](../PRD.md#ac-07---domain-invariant), [AC-09](../PRD.md#ac-09---domain-invariant), [AC-11](../PRD.md#ac-11---cross-context), [AC-18](../PRD.md#ac-18---domain-invariant), [AC-19](../PRD.md#ac-19---authorization), [AC-20](../PRD.md#ac-20---domain-invariant) · **Оцінка:** M

## Why

[Test plan](../PRD.md#test-plan): крайові й помилкові випадки та відтворення змішаного журналу.

## What

Тести: ліміт 3 і 5 extra — наступна доба має 3 нових; паралельні старти; межа доби; практика тієї самої доби не повторюється; зміна часового поясу; завершений курс; відтворення змішаного журналу; доступ до чужих даних; старт плану при активній extra; 30 стартів на хвилину.

Файли: `ConstantLearning.Tests/Learning/`, `ConstantLearning.Tests/Learning/Sessions/`

## Definition of Done

- [ ] усі перелічені AC мають щонайменше один зелений інтеграційний тест на тимчасовій БД
- [ ] набір не залежить від порядку запуску й чистить дані після себе
- [ ] dotnet test без нових попереджень
- [ ] `dotnet build` і тести без нових попереджень
