---
id: T10
title: "Показати дії extra на екрані «Сьогодні» й підтримати extra-заняття в екрані заняття"
layer: "ui"
deps: ["[T09](./T09-extra-endpoints-contract.md)"]
acs: ["AC-01", "AC-04", "AC-08", "AC-10", "AC-16", "AC-17"]
files_hint: ["ConstantLearning.MiniApp/src/features/today/", "ConstantLearning.MiniApp/src/features/session/", "ConstantLearning.MiniApp/src/api/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T10 — Показати дії extra на екрані «Сьогодні» й підтримати extra-заняття в екрані заняття

**Шар:** `ui` · **Залежить від:** [T09](./T09-extra-endpoints-contract.md) · **AC:** [AC-01](../PRD.md#ac-01---happy-path), [AC-04](../PRD.md#ac-04---error), [AC-08](../PRD.md#ac-08---happy-path), [AC-10](../PRD.md#ac-10---error), [AC-16](../PRD.md#ac-16---happy-path), [AC-17](../PRD.md#ac-17---domain-invariant) · **Оцінка:** M

## Why

[PRD US-01..US-03](../PRD.md#4-user-stories); дизайн — наявні `Button`, `Card`, `Result` та токени Everforest з [design-system](../../personal-trainer-mvp/design-system.md).

## What

Замість вимкненої «На сьогодні все» показати стан «план виконано» і дії «Ще одне слово» та «Потренувати вивчені» за полем `extra_actions` (з поясненням причини, коли недоступні). Екран заняття приймає extra-сесії: картка, перевірка, результат, дії «Ще одне слово» і «На головну». Оновити типи API з контракту.

Файли: `ConstantLearning.MiniApp/src/features/today/`, `ConstantLearning.MiniApp/src/features/session/`, `ConstantLearning.MiniApp/src/api/`

## Definition of Done

- [ ] компонентні тести AC-04, AC-10, AC-16, AC-17 зелені; e2e через UI AC-01 і AC-08 проходить
- [ ] наявні тести екрана «Сьогодні» зелені (кнопка «На сьогодні все» лишається лише там, де дій немає)
- [ ] збірка й лінтер без нових попереджень
- [ ] `dotnet build` і тести без нових попереджень
