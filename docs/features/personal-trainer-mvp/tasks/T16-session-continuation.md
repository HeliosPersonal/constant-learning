---
id: T16
title: "Реалізувати продовження session і закриття на межі доби"
layer: "app"
deps: ["[T15](T15-start-session.md)"]
acs: ["AC-24", "AC-25"]
files_hint: ["ConstantLearning/Features/Learning/Sessions/Current/", "ConstantLearning/Features/Learning/Sessions/DayBoundary/", "ConstantLearning.Tests/Learning/Sessions/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T16 — Реалізувати продовження session і закриття на межі доби

**Шар:** `app` · **Залежить від:** [T15](./T15-start-session.md) · **AC:** AC-24, AC-25 · **Оцінка:** M · **Фаза:** 6

## Why

US-05 у [PRD](../PRD.md); потік «Продовження session і межа доби» у [sad §6](../sad.md); `getCurrentSession`, `SessionExpired`.

## What

Повернення наступної неопрацьованої картки; закриття session після кінця доби (показані нові слова лишаються в ліміті, непоказані повертаються в чергу); спільна перевірка «доба session минула» для дій над карткою з відповіддю `session.expired`.

Файли: `ConstantLearning/Features/Learning/Sessions/Current/`, `ConstantLearning/Features/Learning/Sessions/DayBoundary/`, `ConstantLearning.Tests/Learning/Sessions/`

## Definition of Done

- [ ] інтеграційні тести AC-24, AC-25 з `FakeClock` зелені
- [ ] unit-тест спільної перевірки «доба session минула» зелений; інтеграційні тести AC-52 — у [T17](T17-answer-item.md) і [T18](T18-new-word-card.md)
- [ ] `dotnet build`, тести й лінтер без нових попереджень
