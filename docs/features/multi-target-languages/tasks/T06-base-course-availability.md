---
id: T06
title: "Base course на мову + перевірка доступності мови"
layer: "domain"
deps: ["[T01](./T01-config-target-languages.md)", "[T05](./T05-migrate-content-entries.md)"]
acs: ["AC-01", "AC-02", "AC-06"]
files_hint:
  - "ConstantLearning/Features/Content/"
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "done"
---

# T06 — Base course на мову + перевірка доступності мови

**Шар:** domain · **Залежить від:** [T01](./T01-config-target-languages.md), [T05](./T05-migrate-content-entries.md) · **AC:** AC-01, AC-02, AC-06 · **Оцінка:** M · **Фаза:** 3 (Content)

## Why

AC-06 вимагає власний base course зі свого порядку на кожну мову; AC-02 — щоб мова без жодного
схваленого слова лишалася недоступною. Доступність = `is_enabled` ∧ наявний approved-контент
([ADR-0004](../adr/0004-model-supported-languages-as-config-table.md), [api-sync-report §available](../contracts/api-sync-report.md)).

## What

- Добір нових лем для заняття активної мови: `WHERE target_language = ?` у власному порядку
  `course_position` (індекс `ix_dictionary_entries_lang_course_position`, flow 2 [sad.md §6](../sad.md)).
- Обчислюваний прапорець `available` мови: `is_enabled` ∧ `EXISTS` approved `dictionary_entries` цієї мови.
- Завершення base course однієї мови не впливає на добір іншої (AC-06).

## Definition of Done

- [x] Integration-тест: добір нових лем мови A бере тільки записи A у порядку `course_position`; мова B не протікає.
- [x] `available=false` для мови без approved-контенту навіть за `is_enabled=true` (AC-02).
- [x] Завершення курсу A (усі леми видано) не блокує й не зсуває добір B того ж learner (AC-06).
- [x] Unit-тести на обчислення `available` для (enabled/disabled) × (є/нема контенту).
- [x] PR ≤500 LOC.

## Нотатки реалізації

- **Overload замість нового поля.** `IPublishedCourseReader` отримав `ReadAsync(string targetLanguage, ct)`;
  реалізація звелася до приватного `ReadInternalAsync(string? targetLanguage, ct)` з опційним фільтром
  `WHERE target_language = ?` (`null` → усі мови). Запис `PublishedCourseWord` не змінювався — мова не
  потрібна споживачам курсу, лише добору. Безаргументний `ReadAsync()` лишився для глобального lookup за
  `EntryId` (`AnswerContentReader`) і прогріву кешу (`DatabaseInitializationService`).
- **Scoped-виклики.** Сервіси, що добирають план активної мови, передають `current.State.TargetLanguage`
  (`day!.State.TargetLanguage` у `ContinueSessionService`): `NewWordService`, `StartSessionService`,
  `TodayService`, `CurrentSessionService`, `ContinueSessionService`, `StartExtraWordService`,
  `StartExtraPracticeService`. `ReminderLearningReader` читає `learner.ActiveTargetLanguage ?? "pl"` із БД і
  скопує добір на активну мову (`TODO(T13)`: активна мова через `setActiveLanguage`).
- **AnswerContentReader лишається глобальним.** `EntryId` унікальний на всі мови, тож lookup прийнятих
  форм за карткою не скопується мовою — інакше зламався б показ результату картки чужомовного `EntryId`.
- **Доступність мови — чистий хелпер + reader.** `LanguageAvailability.IsAvailable(isEnabled, hasApprovedContent)`
  (без БД, тестується таблицею істинності) та `IApprovedContentLanguages.ReadAsync()` (`SELECT DISTINCT
  target_language` по записах зі схваленою опублікованою версією). Композиція обох джерел у прапорець
  `available` каталогу — на боці викликача в T11.
- **Прибрано мертвий код.** `ExtraPlanGate.PublishedAsync` не мав викликачів і читав усі мови (scoping-пастка) —
  видалено; обидва extra-сервіси читають курс напряму зі scope активної мови.
