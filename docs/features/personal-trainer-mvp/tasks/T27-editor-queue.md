---
id: T27
title: "Реалізувати чергу й рішення content editor"
layer: "app"
deps: ["[T23](T23-content-report-api.md)", "[T20](T20-dictionary-import.md)", "[T21](T21-search-forms.md)"]
acs: ["AC-38", "AC-39", "AC-40", "AC-41", "AC-42", "AC-51"]
files_hint: ["ConstantLearning/Features/Content/Editor/Queue/", "ConstantLearning.Tests/Content/Editor/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T27 — Реалізувати чергу й рішення content editor

**Шар:** `app` · **Залежить від:** [T23](./T23-content-report-api.md), [T20](./T20-dictionary-import.md), [T21](./T21-search-forms.md) · **AC:** AC-38, AC-39, AC-40, AC-41, AC-42, AC-51 · **Оцінка:** M · **Фаза:** 8

## Why

US-10 у [PRD](../PRD.md); потік «Розгляд черги content editor» у [sad §6](../sad.md); `listEditorQueue`, `decideContentReport`.

## What

Курсорна черга з псевдонімами, рішення approve / reject з причиною / add_to_candidates, закриття report і результат у підсумок learner.

Файли: `ConstantLearning/Features/Content/Editor/Queue/`, `ConstantLearning.Tests/Content/Editor/`

## Definition of Done

- [ ] інтеграційні тести AC-38…AC-42 зелені
- [ ] повтор report за AC-51 видно в черзі одним рядком
- [ ] відповідь черги містить лише псевдонімну мітку автора без імені й Telegram id
- [ ] повторне рішення повертає `report.already_closed`
- [ ] `dotnet build`, тести й лінтер без нових попереджень
