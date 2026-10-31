---
id: T12
title: "Автентифікувати запити за initData і додати роль content editor"
layer: "ports"
deps: ["T03", "T02"]
acs: ["AC-29", "AC-41", "AC-50"]
files_hint: ["ConstantLearning/Features/Identity/Auth/", "ConstantLearning.Tests/Identity/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T12 — Автентифікувати запити за initData і додати роль content editor

**Шар:** `ports` · **Залежить від:** [T03](./T03-migrate-identity.md), [T02](./T02-test-harness.md) · **AC:** AC-29, AC-41, AC-50 · **Оцінка:** M · **Фаза:** 3

## Why

[ADR-0005](../adr/0005-authenticate-every-api-request-with-telegram-init-data.md), авторизація — [sad §8](../sad.md#8-crosscutting-concepts).

## What

`AuthenticationHandler` для `Authorization: tma <initData>` (HMAC-SHA256, `auth_date` ≤ 24 год), пошук learner за Telegram id, `IClaimsTransformation` з роллю content editor за конфігурацією, policy `ContentEditor`, конверт помилки `auth.invalid_init_data`.

Файли: `ConstantLearning/Features/Identity/Auth/`, `ConstantLearning.Tests/Identity/`

## Definition of Done

- [ ] тести: валідний підпис — 200, підроблений чи прострочений — `auth.invalid_init_data`
- [ ] learner id береться лише з claims; запит до чужого ресурсу — «не знайдено» (AC-29)
- [ ] `initData` не потрапляє в логи
- [ ] `dotnet build`, тести й лінтер без нових попереджень
