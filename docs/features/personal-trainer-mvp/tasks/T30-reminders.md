---
id: T30
title: "Реалізувати планування й умови нагадувань"
layer: "app"
deps: ["T29", "T08", "T14", "T16"]
acs: ["AC-26", "AC-44"]
files_hint: ["ConstantLearning/Features/Notifications/Reminders/", "ConstantLearning.Tests/Notifications/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T30 — Реалізувати планування й умови нагадувань

**Шар:** `app` · **Залежить від:** [T29](./T29-notification-dispatcher.md), [T08](./T08-learner-day.md), [T14](./T14-settings-api.md), [T16](./T16-session-continuation.md) · **AC:** AC-26, AC-44 · **Оцінка:** M · **Фаза:** 8

## Why

US-06 у [PRD](../PRD.md); Critical flow 2 у [sad §6](../sad.md); умови `reminder` в [events.md](../contracts/events.md); QG-3.

## What

Planner створює відправлення на добу learner і під'єднується до точки «налаштування змінено» з T14, щоб перераховувати їх; умови: пропуск, відкладення на 30 хв після останньої відповіді (не пізніше 2 год і кінця доби), два види тексту.

Файли: `ConstantLearning/Features/Notifications/Reminders/`, `ConstantLearning.Tests/Notifications/`

## Definition of Done

- [ ] тести з `FakeClock` на всі гілки AC-26 зелені
- [ ] зміна поясу не дає другого нагадування за добу (AC-44)
- [ ] тест переходу DST зелений
- [ ] `dotnet build`, тести й лінтер без нових попереджень
