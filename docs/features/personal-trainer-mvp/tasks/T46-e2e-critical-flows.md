---
id: T46
title: "Покрити наскрізними тестами критичні потоки через UI"
layer: "tests"
deps: ["[T35](T35-ui-onboarding.md)", "[T38](T38-ui-recall.md)", "[T40](T40-ui-content-report.md)", "[T41](T41-ui-settings.md)", "[T45](T45-deployment-fixes.md)"]
acs: ["AC-01", "AC-03", "AC-12", "AC-17", "AC-34"]
files_hint: ["ConstantLearning.MiniApp/e2e/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T46 — Покрити наскрізними тестами критичні потоки через UI

**Шар:** `tests` · **Залежить від:** [T35](./T35-ui-onboarding.md), [T38](./T38-ui-recall.md), [T40](./T40-ui-content-report.md), [T41](./T41-ui-settings.md), [T45](./T45-deployment-fixes.md) · **AC:** AC-01, AC-03, AC-12, AC-17, AC-34 · **Оцінка:** M · **Фаза:** 11

## Why

Тест-план (`/sdlc:plan-tests`) і критичні потоки [sad §6](../sad.md).

## What

Playwright з `mockTelegramEnv` і тестовим підписувачем `initData` (тестовий токен бота) проти локального бекенду на Testcontainers. Сценарії запускаються локально, не в CI. Перед пілотом виконується один ручний smoke test у Telegram з тестовим ботом і HTTPS-тунелем.

Файли: `ConstantLearning.MiniApp/e2e/`

## Definition of Done

- [ ] сценарій AC-01: онбординг веде на «Сьогодні»
- [ ] сценарій AC-03: заняття показує повторення перед новими словами
- [ ] сценарій AC-12: правильна відповідь зараховується
- [ ] сценарій AC-17: оскарження знімає mistake
- [ ] сценарій AC-34: content report показує «Дякуємо, передали на перевірку»
- [ ] усі сценарії проходять локально
- [ ] перед пілотом один раз перевірено запуск Mini App у Telegram з тестовим ботом і HTTPS-тунелем
- [ ] `dotnet build`, тести й лінтер без нових попереджень
