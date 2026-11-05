---
id: T10
title: "Ізоляція обліку прогресу за (learner, мова)"
layer: "domain"
deps: ["[T04](./T04-per-language-day-state.md)", "[T08](./T08-migrate-learner-words-answer-events.md)", "[T09](./T09-migrate-sessions-reports.md)"]
acs: ["AC-04", "AC-05", "AC-10"]
files_hint:
  - "ConstantLearning/Features/Learning/"
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "done"
---

# T10 — Ізоляція обліку прогресу за `(learner, мова)`

**Шар:** domain · **Залежить від:** [T04](./T04-per-language-day-state.md), [T08](./T08-migrate-learner-words-answer-events.md), [T09](./T09-migrate-sessions-reports.md) · **AC:** AC-04, AC-05, AC-10 · **Оцінка:** L · **Фаза:** 4 (Learning)

## Why

Це серце фічі й **ризик №1** ([SAD §8](../sad.md), [test-plan.md §Ізоляція](../test-plan.md)):
кожен запит прогресу/квоти має фільтрувати за `(learner_id, target_language)`, інакше відповідь
зараховується не своїй мові (AC-04), ліміти/«Вже знаю» змішуються (AC-05) або learner бачить чужий
прогрес (AC-10). Денормалізований `target_language` (T08/T09) має використовуватися в кожному фільтрі.

## What

- Протягнути `target_language` у фільтри: словесний прогрес (word status, due), складання session,
  оцінку відповіді, облік дії «Вже знаю», добовий ліміт нових слів — усі за `(learner, активна мова)`.
- Відповідь зараховується лише `target_language` своєї session; картка не переноситься між мовами (AC-04).
- Інваріант: `target_language` рядка прогресу = `target_language` його `entry` — app-перевірка + тест.
- Крос-learner приховування (AC-10): прогрес чужого learner будь-якою мовою не повертається й не існує для запитувача.

## Definition of Done

- [x] Integration-тест: відповідь у мові A не змінює жоден лічильник мови B (0 витоків — NFR ізоляції).
- [x] Тест: вичерпаний ліміт/«Вже знаю» у A не впливає на видачу B того ж learner (AC-05).
- [x] Тест: незавершена session A лишається доступною після перемикання на B; картки не мігрують (AC-04).
- [x] Тест: learner 2 не бачить прогрес learner 1 у жодній мові (AC-10, приховування існування).
- [x] Інваріант «мова рядка = мова entry» покрито тестом; жоден гарячий запит не робить join заради мови.
- [x] PR ≤500 LOC.

## Нотатки реалізації

Продакшн-код мово-свідомості звели ще в межах підготовки T10: кожен сесійний сервіс несе активну мову в
усі читання прогресу й записи. `StartSessionService`/`NewWordService`/`StartExtraPracticeService` беруть
`lang = current.State.TargetLanguage`, `AnswerSessionService` — `lang = session.TargetLanguage` (відповідь
належить мові своєї session, AC-04). `SessionLimitsService.ReadAsync` і кожен `db.LearnerWords`/
`db.AnswerEvents` фільтр звужують за `TargetLanguage == lang` по денормалізованій колонці (ADR-0001) —
**без join до `dictionary_entries` заради мови**, тож гарячий шлях лишається одно-табличним.

Рішення, зафіксовані тут:

- **Дефолт `= "pl"` на сутностях лишено, не зроблено `required`.** `LearnerWord`/`AnswerEvent`/`LearningSession`/
  `DictionaryEntry` мають інтеримний `TargetLanguage = "pl"` як запобіжник для ще-не-мово-свідомих тест-фабрик;
  продакшн-запис завжди проставляє мову явно. Знімається разом з інтеримними TODO (T07a/T13).
- **Активна мова резолвиться як `targetLanguage ?? learner.ActiveTargetLanguage ?? "pl"`** (interim, `// TODO(T13)`
  у `LearnerDayStateService`). Жодного gate увімкнення мови в сесійних сервісах — його місце у [T13](./T13-set-active-scope-existing.md).
- **`ExtraPlanGate.RateLimitedAsync` і `LearningProgressResetHandler` навмисно крос-мовні** — rate-limit «потренувати»
  та скидання прогресу стосуються learner цілком, не однієї мови; це не витік, а свідоме рішення.

Тести живуть у `ConstantLearning.Tests/Learning/Isolation/ProgressIsolationTests.cs` (5 `[DockerFact]`, по одному
на пункт DoD). Живий pl-потік ганяється через HTTP-хост `AnswerTestApplication` (базовий `Services` не реєструє
`AddLearning`), мово-сусідні en-рядки засіваються напряму в `Db` й перевіряються на недоторканість на рівні таблиць.
Перемикання активної мови — через `Learner.ActiveTargetLanguage`; крос-learner приховування — очікуваний `404
session.item_not_found` на чужі session/картку (а не `403`).
