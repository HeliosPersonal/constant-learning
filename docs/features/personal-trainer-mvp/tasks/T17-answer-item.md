---
id: T17
title: "Реалізувати відповідь на картку з ідемпотентністю"
layer: "app"
deps: ["[T09](T09-answer-evaluation.md)", "[T10](T10-review-scheduler.md)", "[T16](T16-session-continuation.md)"]
acs: ["AC-12", "AC-13", "AC-14", "AC-15", "AC-16", "AC-33", "AC-42", "AC-52", "AC-56", "AC-57"]
files_hint: ["ConstantLearning/Features/Learning/Sessions/Answer/", "ConstantLearning.Tests/Learning/Sessions/"]
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T17 — Реалізувати відповідь на картку з ідемпотентністю

**Шар:** `app` · **Залежить від:** [T09](./T09-answer-evaluation.md), [T10](./T10-review-scheduler.md), [T16](./T16-session-continuation.md) · **AC:** AC-12, AC-13, AC-14, AC-15, AC-16, AC-33, AC-42, AC-52, AC-56, AC-57 · **Оцінка:** L · **Фаза:** 7

## Why

US-03 у [PRD](../PRD.md); Critical flow 1 і «Оцінка відповіді й статус слова» в [sad §6](../sad.md); `answerSessionItem`; QG-1.

## What

В одній транзакції: захоплення картки `pending → answered`, оцінка ([T09](T09-answer-evaluation.md)), запис `answer_events`, виклик планувальника ([T10](T10-review-scheduler.md)) і побудова наступної картки та `session_completed`; повтор з тим самим вмістом повертає збережений результат, з іншим — `answer.already_answered_differently`; картка запису, що втратив `approved`, пропускається без зарахування (AC-33); після межі доби — `session.expired` (AC-52).

Файли: `ConstantLearning/Features/Learning/Sessions/Answer/`, `ConstantLearning.Tests/Learning/Sessions/`

## Межі реалізації

Поточний approved-контент, прийняті форми й канонічні підказки інших записів надходять через Content port `IAnswerContentReader`; Learning не читає таблиці Content напряму. `SessionItem.ResponsePayload` у [моделі даних](../data-model.md) зберігає точний результат команди в тій самій транзакції, що й подія та стан слова. Повтор не викликає оцінювання чи планувальник повторно. Після межі доби спільний guard із [T16](T16-session-continuation.md) зберігає закриття перед відповіддю 409.

Адаптер `AnswerWordState` читає та записує проєкцію [планувальника](T10-review-scheduler.md), а `state_before` містить версіонований знімок полів слова й `model_state` для майбутнього [оскарження](T24-answer-dispute.md). Перевірка нового слова `new_word_check` не змінює запланований стан. Окремі регресії переходів статусу й control repetition належать [T48](T48-word-status-transitions.md).

Якщо поточне слово втратило `approved`, сервер атомарно позначає картку `skipped` і повертає HTTP 409 з кодом `answer.content_unavailable`, без події відповіді чи зміни стану слова. Клієнт після цієї відмови перечитує `GET /api/v1/sessions/current`, щоб перейти до доступної картки або повернутися на «Сьогодні»; цей перехід має врахувати [клієнт заняття](T38-ui-recall.md).

Фактичний Release-прогін 200 відповідей на фікстурі з 1 000 слів описано у [звіті вимірювання](../test-output/session-answer-benchmark.md), зі [сирими вимірюваннями](../test-output/session-answer-samples.csv).

## Definition of Done

- [ ] інтеграційний тест подвійного надсилання дає рівно одну подію відповіді (QG-1)
- [ ] інтеграційні тести AC-12…AC-16, AC-33, AC-52, AC-56, AC-57 зелені
- [ ] після публікації нової версії фікстурою картка й оцінка використовують новий переклад, word status не змінюється (AC-42)
- [ ] чужі session чи картка — «не знайдено»
- [ ] бенчмарк: 200 послідовних відповідей на фікстурі з 1 000 слів learner — p95 обробки на сервері ≤ 500 ms
- [ ] `dotnet build`, тести й лінтер без нових попереджень
