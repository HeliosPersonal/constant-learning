---
id: T14
title: "Реалізувати зміну ліміту, поясу й нагадувань"
layer: "app"
deps: ["[T13](T13-onboarding-api.md)", "[T08](T08-learner-day.md)"]
acs: ["AC-27", "AC-44", "AC-45"]
files_hint: ["ConstantLearning/Features/Identity/Settings/", "ConstantLearning.Tests/Identity/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T14 — Реалізувати зміну ліміту, поясу й нагадувань

**Шар:** `app` · **Залежить від:** [T13](./T13-onboarding-api.md), [T08](./T08-learner-day.md) · **AC:** AC-27, AC-44, AC-45 · **Оцінка:** M · **Фаза:** 5

## Why

US-11 у [PRD](../PRD.md); потік «Налаштування темпу, поясу й нагадувань» у [sad §6](../sad.md); `updateLearnerSettings`.

## What

Ліміт лише 3/5/10 і з наступної доби learner (`pending_daily_new_words_limit`), зміна поясу одразу, reminder лише з вибраним поясом; точка розширення «налаштування змінено», до якої [T30](T30-reminders.md) під'єднує перерахунок відправлень.

Файли: `ConstantLearning/Features/Identity/Settings/`, `ConstantLearning.Tests/Identity/`

## Definition of Done

- [ ] інтеграційні тести AC-27, AC-45 зелені
- [ ] AC-44 у частині ліміту й поясу: новий ліміт не діє в поточну добу learner, пояс змінюється одразу; частину про нагадування перевіряє [T30](T30-reminders.md)
- [ ] `dotnet build`, тести й лінтер без нових попереджень
