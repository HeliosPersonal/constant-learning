---
id: T09
title: "Реалізувати оцінку відповіді з нормалізацією й різницею написання"
layer: "domain"
deps: ["T01"]
acs: ["AC-12", "AC-13", "AC-14", "AC-15", "AC-16", "AC-56", "AC-57"]
files_hint: ["ConstantLearning/Features/Learning/Domain/Evaluation/", "ConstantLearning.Tests/Learning/Domain/Evaluation/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T09 — Реалізувати оцінку відповіді з нормалізацією й різницею написання

**Шар:** `domain` · **Залежить від:** [T01](./T01-module-skeleton.md) · **AC:** AC-12, AC-13, AC-14, AC-15, AC-16, AC-56, AC-57 · **Оцінка:** M · **Фаза:** 2

## Why

AC-12…AC-16 і правило «Прийняті варіанти» з [PRD §5](../PRD.md#5-acceptance-criteria); нормалізація — [sad §8 Answer evaluation](../sad.md#8-crosscutting-concepts).

## What

Чиста функція: NFC, обрізання й стискання пробілів, нижній регістр, прибирання кінцевої пунктуації, заміна кириличних двійників, порівняння з прийнятими варіантами, відмінність лише в діакритиці, синонім з іншого запису (`synonym`), залишок кирилиці (`answer.wrong_layout`), структурований diff.

Файли: `ConstantLearning/Features/Learning/Domain/Evaluation/`, `ConstantLearning.Tests/Learning/Domain/Evaluation/`

## Definition of Done

- [ ] unit-тести на кожен результат AC-12…AC-16 зелені
- [ ] unit-тести AC-56, AC-57 і таблиця нормалізації (пунктуація, кириличні двійники) зелені
- [ ] diff однаковий для однакових пар відповідей
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

OQ-5 закрито: правило «Прийняті варіанти» в PRD §5, AC-56, AC-57.
