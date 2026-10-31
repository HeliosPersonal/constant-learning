---
id: T01
title: "Створити каркас feature-модулів, Platform і NodaTime"
layer: "wiring"
deps: []
acs: []
files_hint: ["ConstantLearning/Features/", "ConstantLearning/Platform/", "ConstantLearning/Data/AppDbContext.cs", "ConstantLearning/Data/AppDbContextFactory.cs", "ConstantLearning/Program.cs", "ConstantLearning/ConstantLearning.csproj", "ConstantLearning.Tests/Architecture/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T01 — Створити каркас feature-модулів, Platform і NodaTime

**Шар:** `wiring` · **Залежить від:** — · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 1

## Why

Модульний моноліт з [ADR-0003](../adr/0003-organise-code-into-feature-modules-inside-the-monolith.md) і час learner з [ADR-0006](../adr/0006-model-learner-time-with-nodatime-and-iana-zones.md); структура — [sad §5](../sad.md).

## What

Папки `Features/{Identity,Learning,Content,Notifications,Bot}` з порожніми `Add<Module>()`, `Platform/` з `IClock` (NodaTime), налаштуванням Npgsql + NodaTime і `snake_case`-іменуванням EF. Перенести `AppDbContext` і `AppDbContextFactory` у `Platform/Persistence/`. Архітектурний тест: модуль не звертається до внутрішніх типів іншого модуля.

Файли: `ConstantLearning/Features/`, `ConstantLearning/Platform/`, `ConstantLearning/Data/AppDbContext.cs`, `ConstantLearning/Data/AppDbContextFactory.cs`, `ConstantLearning/Program.cs`, `ConstantLearning/ConstantLearning.csproj`, `ConstantLearning.Tests/Architecture/`

## Definition of Done

- [ ] `dotnet build` і наявні тести зелені
- [ ] архітектурний тест падає на навмисному порушенні межі модуля й проходить без нього
- [ ] `IClock` зареєстровано, `DateTime.Now` у нових модулях не використовується
- [ ] `dotnet build`, тести й лінтер без нових попереджень
