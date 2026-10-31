---
id: T32
title: "Переробити webhook бота під Mini App"
layer: "ports"
deps: ["T03"]
acs: []
files_hint: ["ConstantLearning/Features/Bot/", "ConstantLearning/Platform/Telegram/", "ConstantLearning/Controllers/TelegramController.cs", "ConstantLearning/HostedServices/WebhookConfigurationService.cs", "ConstantLearning.Tests/Bot/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T32 — Переробити webhook бота під Mini App

**Шар:** `ports` · **Залежить від:** [T03](./T03-migrate-identity.md) · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 3

## Why

[sad §8 Bot webhook](../sad.md#8-crosscutting-concepts); `receiveTelegramUpdate` в [openapi.yaml](../contracts/openapi.yaml); обмеження з [architecture-map.md](../../../architecture-map.md).

## What

Обгортка Telegram-клієнта в `Platform/Telegram/` для всіх модулів; `/start` з кнопкою Mini App, `secret_token` і перевірка заголовка, явні `allowed_updates`, `my_chat_member` оновлює `can_message`, webhook не видаляється на зупинці.

Файли: `ConstantLearning/Features/Bot/`, `ConstantLearning/Platform/Telegram/`, `ConstantLearning/Controllers/TelegramController.cs`, `ConstantLearning/HostedServices/WebhookConfigurationService.cs`, `ConstantLearning.Tests/Bot/`

## Definition of Done

- [ ] тест: запит без правильного секрету — `webhook.invalid_secret`
- [ ] тест: блокування бота вимикає `can_message`
- [ ] `dotnet build`, тести й лінтер без нових попереджень
