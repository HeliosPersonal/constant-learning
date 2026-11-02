---
id: T02
title: "Додати інтеграційні тести на PostgreSQL з FakeClock і фікстурами"
layer: "tests"
deps: ["[T01](T01-module-skeleton.md)"]
acs: []
files_hint: ["ConstantLearning.Tests/Infrastructure/", "ConstantLearning.Tests/Fixtures/", "ConstantLearning.Tests/ConstantLearning.Tests.csproj"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T02 — Додати інтеграційні тести на PostgreSQL з FakeClock і фікстурами

**Шар:** `tests` · **Залежить від:** [T01](./T01-module-skeleton.md) · **AC:** — (технічна передумова) · **Оцінка:** M · **Фаза:** 2

## Why

QG-1 у [sad §10](../sad.md) вимагає перевірки на справжньому PostgreSQL; фікстури описано в [data-model.md](../data-model.md#test-fixtures).

## What

Базовий клас інтеграційного тесту з ефемерним PostgreSQL 16, застосуванням міграцій, `FakeClock`, тестовим підписувачем `initData` і фабриками `LearnerFixtures`, `ContentFixtures`, `LearningFixtures`; contract-перевірка, що валідує відповіді API проти `contracts/openapi.yaml` і рядки `verification_jobs` / `notification_dispatches` проти `contracts/events.md`.

Файли: `ConstantLearning.Tests/Infrastructure/`, `ConstantLearning.Tests/Fixtures/`, `ConstantLearning.Tests/ConstantLearning.Tests.csproj`

## Definition of Done

- [ ] приклад інтеграційного тесту піднімає контейнер, застосовує міграції й проходить
- [ ] contract-перевірка ловить відповідь, що не збігається зі схемою
- [ ] локально без Docker інтеграційні тести пропускаються з поясненням, а в CI пропуск вважається падінням
- [ ] `dotnet build`, тести й лінтер без нових попереджень

## Notes

Фікстури доповнюються в задачах, яким вони потрібні.
