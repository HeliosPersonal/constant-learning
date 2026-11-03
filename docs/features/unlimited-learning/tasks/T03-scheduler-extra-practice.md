---
id: T03
title: "Додати третій вид вправи ExtraPractice у планувальник і проєктор"
layer: "domain"
deps: []
acs: ["AC-12", "AC-13", "AC-14", "AC-15"]
files_hint: ["ConstantLearning/Features/Learning/Domain/Scheduling/", "ConstantLearning.Tests/Learning/Domain/Scheduling/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T03 — Додати третій вид вправи ExtraPractice у планувальник і проєктор

**Шар:** `domain` · **Залежить від:** — · **AC:** [AC-12](../PRD.md#ac-12---domain-invariant), [AC-13](../PRD.md#ac-13---domain-invariant), [AC-14](../PRD.md#ac-14---domain-invariant), [AC-15](../PRD.md#ac-15---domain-invariant) · **Оцінка:** M

## Why

[ADR-0002](../adr/0002-model-extra-practice-as-a-third-exercise-kind-in-the-scheduler.md): правило практики живе в порту планувальника; журнал — джерело істини ([ADR-0009](../../personal-trainer-mvp/adr/0009-schedule-reviews-with-memorize-behind-a-port-over-an-answer-log.md)).

## What

Додати `ExtraPractice` у `ReviewExerciseKind`. `Apply`: слово не мусить бути до повторення; правильна відповідь збільшує лічильник лише якщо остання спроба не сьогодні, не міняє дату, інтервал і швидкість забування; помилка діє як у плановому повторенні; синонім лише позначає спробу. Проєктор і нейтралізація відновлюють стан і виключають слово до кінця доби. Версія планувальника не змінюється.

Файли: `ConstantLearning/Features/Learning/Domain/Scheduling/`, `ConstantLearning.Tests/Learning/Domain/Scheduling/`

## Definition of Done

- [ ] юніт-тести правил практики зелені (правильна відповідь не відсуває дату, помилка знижує статус, спроба цієї доби не підвищує)
- [ ] відтворення змішаного журналу (планові, перевірки, практика, нейтралізація) дає той самий стан, що збережений
- [ ] поведінка наявних видів вправ не змінилась (наявні тести планувальника зелені)
- [ ] `dotnet build` і тести без нових попереджень
