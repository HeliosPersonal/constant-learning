---
id: T47
title: "Застосувати денний ліміт, зміну поясу й квоти під час старту session"
layer: "app"
deps: ["[T15](T15-start-session.md)", "[T08](T08-learner-day.md)"]
acs: ["AC-06", "AC-07", "AC-09", "AC-10", "AC-33", "AC-54"]
files_hint: ["ConstantLearning/Features/Learning/Sessions/Start/Limits/", "ConstantLearning.Tests/Learning/Sessions/Limits/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T47 — Застосувати денний ліміт, зміну поясу й квоти під час старту session

**Шар:** `app` · **Залежить від:** [T15](./T15-start-session.md), [T08](./T08-learner-day.md) · **AC:** AC-06, AC-07, AC-09, AC-10, AC-33, AC-54 · **Оцінка:** M · **Фаза:** 6

## Why

Правила «Доба learner», «Зміна часового поясу» і «Склад session» з [PRD §5](../PRD.md#5-acceptance-criteria); потік «Складання session» у [sad §6](../sad.md).

## What

Залишок денного ліміту за добою learner ([T08](T08-learner-day.md)) з урахуванням виданих і «Вже знаю» понад 10, частки квот з БД, стан «курс завершено» і повідомлення session.

Файли: `ConstantLearning/Features/Learning/Sessions/Start/Limits/`, `ConstantLearning.Tests/Learning/Sessions/Limits/`

## Межі реалізації

Старт використовує збережену добу й дозрілий pending limit через Identity port із [T14](T14-settings-api.md). Ліміти читають лише підтверджені покази та події «Вже знаю»; складання чи повторне повернення session не витрачає ліміт. Запис показів належить [T18](T18-new-word-card.md).

Стійкий nullable маркер `course_completed_at` у [моделі даних](../data-model.md#агрегат-identity--learners) змінюється атомарно зі стартом session. Повідомлення `course_replenished` повертається один раз на перехід із завершеного курсу до доступних нових лем, зокрема при конкурентних викликах і перезапуску застосунку. Воно вже передбачене [контрактом API](../contracts/openapi.yaml). Спільний `SessionLimitsService` може використовувати майбутня реалізація екрана «Сьогодні» у [T19](T19-today-api.md), утримуючи те саме блокування learner; поточний старт повертає повідомлення в `Session.notices`.

## Definition of Done

- [ ] інтеграційні тести AC-06, AC-07, AC-09, AC-10, AC-54 з `FakeClock` зелені
- [ ] запис без `approved` не потрапляє в нову session, а після повторного схвалення слово повертається з тією самою історією (AC-33)
- [ ] поява нових approved-лем після «курс завершено» знову дає нові слова й одноразове повідомлення «курс поповнено» (AC-10)
- [ ] `dotnet build`, тести й лінтер без нових попереджень
