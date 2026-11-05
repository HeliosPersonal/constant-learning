---
id: T04
title: "Per-language стан доби learner (ILearnerDayStateService)"
layer: "domain"
deps: ["[T03](./T03-migrate-identity-day-state.md)"]
acs: ["AC-04", "AC-05", "AC-07"]
files_hint:
  - "ConstantLearning/Features/Identity/"
  - "ConstantLearning.Tests/"
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "done"
---

# T04 — Per-language стан доби learner (`ILearnerDayStateService`)

**Шар:** domain · **Залежить від:** [T03](./T03-migrate-identity-day-state.md) · **AC:** AC-04, AC-05, AC-07 · **Оцінка:** L · **Фаза:** 2 (Identity)

## Why

[ADR-0002](../adr/0002-scope-day-state-per-target-language.md) переносить стан доби з `learners`
у `learner_language_states`: блокування береться на рядок **(learner_id, target_language)**, не на
learner. Це змінює конвенцію `ILearnerDayStateService` (SAD §5) і робить видачу нових слів та
обчислення доби (зі зміною поясу) незалежними на мову — підґрунтя AC-04/AC-05/AC-07.

## What

- Переписати `ILearnerDayStateService` на ключ `(learner_id, target_language)`: читання/просування
  доби, обчислення меж зі зміною часового поясу — з рядка `learner_language_states`.
- Dual-write проміжок: сервіс пише в нову таблицю (стара на `learners` лишається до contract-фази).
- Обчислення `new_words_available_from`, `current_day_*`, `course_completed_at` — per-language.
- Блокування рядка день-стану під час мутації (узгодити з конкурентністю T12 / OQ-4).

## Definition of Done

- [x] Unit-тести обчислення доби зі зміною поясу — зелені для довільної мови (не лише `pl`).
- [x] Integration-тест: просування доби мови A не торкає рядок день-стану мови B того ж learner (AC-05).
- [x] Блокування береться на `(learner_id, target_language)`; паралельна мутація різних мов не серіалізується зайве.
- [x] Порожній стан нового рядка мови дає коректний «перший день» (AC-07).
- [x] PR ≤500 LOC.

## Нотатки реалізації

- **Ключ стану доби** — `LearnerDayState` тепер несе `LearnerLanguageState State` поряд із `Learner`:
  весь облік доби (ліміт нових слів, `current_day_*`, `new_words_available_from`, `course_completed_at`)
  читається/пишеться з рядка `learner_language_states`, а `Learner` лишається носієм поясу й профілю.
- **Блокування — лише на рядок мови.** `ResolveForUpdateAsync` більше не бере `FOR UPDATE` на `learners`;
  pin береться на `(learner_id, target_language)` у `LockOrCreateStateAsync`. Learner читається без
  блокування (задля поясу й активної мови), тож мутації різних мов не серіалізуються (DoD #3, ADR-0002).
- **Лінива матеріалізація рядка мови (AC-07).** `FOR UPDATE` на неіснуючому рядку не бере блокування й
  повертає 0 рядків, тому спершу `INSERT ... SELECT ... ON CONFLICT (learner_id, target_language) DO NOTHING`
  (ідемпотентно, безпечно до конкурентного старту), потім перечитування з `FOR UPDATE`. Новий рядок
  інтеримно засівається поточним станом доби learner; порожня доба → коректний «перший день».
  `TODO(T12)`: `startLearnerLanguage` створюватиме рядок нової мови з власними дефолтами ДО першого виклику.
- **xmin-перечитування під блокуванням.** Learner читаємо ДО блокування рядка мови, тож перед
  дзеркаленням активної мови перечитуємо learner уже під `FOR UPDATE` рядка мови — свіжий `xmin`
  лишається чинним до commit і дзеркало не падає на `DbUpdateConcurrencyException`.
- **Інтеримне дзеркало (`AppDbContext.SaveChanges`).** Стан доби АКТИВНОЇ мови (`ActiveTargetLanguage ?? "pl"`)
  дзеркалиться на колонки `learners`, поки їх читають off-path читачі (`ReminderAudienceReader`,
  `NotificationAudienceReader`, `LearnersController`, `LearnerResponse`). Пишемо `learners` лише для
  активної мови, тож мутації неактивних мов не чіпають спільний рядок. `TODO(T10/T13)`: знімається в
  contract-фазі, коли читачі перейдуть на `learner_language_states`.
- **Інтеримна активна мова** — `learner.ActiveTargetLanguage ?? "pl"` (`TODO(T13)`: визначатиметься
  контекстом запиту через `setActiveLanguage`).
- **Call-sites.** `current.Learner.X` → `current.State.X` у `SessionLimitsService`, `NewWordService`,
  `StartSessionService`, `TodayService`, `ExtraPlanGate`, `LearnerSettingsService`, `ProgressResetService`
  (поля обліку доби), пояс (`TimeZone`) лишається на `Learner`.
- **Тести** — `LearnerLanguageDayStateTests` (перший день не-`pl` мови, ізоляція доби A↔B (AC-05),
  per-language блокування); `NewWordTests` оновлено на авторитетний рядок `learner_language_states`.
