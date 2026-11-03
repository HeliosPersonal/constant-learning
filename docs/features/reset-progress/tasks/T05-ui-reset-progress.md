---
id: T05
title: "Додати кнопку, діалог підтвердження й обробку скинутого стану в Mini App"
layer: "ui"
deps: ["[T04](./T04-reset-endpoint.md)"]
acs: ["AC-02", "AC-03", "AC-07", "AC-08"]
files_hint: ["ConstantLearning.MiniApp/src/features/settings/", "ConstantLearning.MiniApp/src/features/session/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T05 — Додати кнопку, діалог підтвердження й обробку скинутого стану в Mini App

**Шар:** `ui` · **Залежить від:** [T04](./T04-reset-endpoint.md) · **AC:** AC-02, AC-03, AC-07, AC-08 · **Оцінка:** M

## Why

[PRD US-02, US-04](../PRD.md#4-user-stories); дизайн — наявні `Button`, `Card`, `Dialog`, `Result` та токени Everforest з [design-system](../../personal-trainer-mvp/design-system.md).

## What

Окрема секція внизу «Налаштування», не поруч зі «Зберегти зміни»: кнопка скидання відкриває діалог із переліком стертого й збереженого та попередженням про незворотність, кнопки «Скасувати» і «Стерти прогрес». Після успіху — скидання кешу learner і «Сьогодні», повідомлення про результат. Відповідь на стерту картку (існуючий код «картку не знайдено») веде на «Сьогодні» з повідомленням «Заняття більше немає — можливо, прогрес скинуто». Нових примітивів не створювати.

Файли: `ConstantLearning.MiniApp/src/features/settings/`, `ConstantLearning.MiniApp/src/features/session/`

## Definition of Done

- [ ] компонентні тести AC-02, AC-03, AC-08 зелені; e2e через UI AC-01 і AC-07 проходить
- [ ] `dotnet build`, тести й лінтер без нових попереджень
