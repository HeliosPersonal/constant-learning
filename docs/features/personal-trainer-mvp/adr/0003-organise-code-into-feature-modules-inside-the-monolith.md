---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0003 — Organise code into feature modules inside the monolith

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

Поточний код розкладено технічними папками (`Controllers`, `Services`, `Jobs`) і прив'язано до `ChatId`. Майже весь код персонального тренера новий; відповідь, розклад слова й session змінюються в одній транзакції.

## Decision drivers

- NFR «0 втрачених відповідей» вимагає однієї транзакції на відповідь (PRD §6)
- Конвенції DI в `Program.cs`, primary constructors, `IOptions<T>` (`architecture-map.md`)
- Один розробник, один екземпляр

## Considered options

1. **Технічні папки** — Нове додається до `Controllers`, `Services`, `Jobs` поруч зі старим груповим кодом.
2. **Feature-модулі в одному проєкті** — `Features/Identity`, `Learning`, `Content`, `Notifications`, `Bot` + `Platform`, один `AppDbContext`.
3. **Окремі class library на модуль** — Межі перевіряє компілятор через `internal`.

## Decision outcome

**Chosen:** Option 2. Модулі відділяють новий код від групової моделі й групують фічі, а один `DbContext` зберігає транзакційність; до окремих збірок можна перейти, коли межі устаткуються.

## Consequences

**Positive**
- Новий код не змішується з `ChatId`-логікою
- Кожен модуль реєструється одним `Add<Module>()`

**Negative**
- Межі тримаються на домовленості й архітектурному тесті, а не на компіляторі
- Спільний `AppDbContext` дозволяє JOIN між модулями
- `architecture-map.md` треба оновити після реалізації

**Neutral**
- Перехід на окремі збірки пізніше — механічний, якщо дотримано правило доступу через інтерфейси

## Links

- PRD: [[../PRD.md]]
- SAD: [[../sad.md]] §5
- Related ADR: [[0004-retire-the-group-model-without-data-migration]]
