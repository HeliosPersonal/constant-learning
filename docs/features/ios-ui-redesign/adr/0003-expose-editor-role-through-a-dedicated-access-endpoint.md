---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-11-04"
feature_size: M
stage: "04-05"
ticket: "TBD"
---

# 0003 — Повідомляти роль редактора окремим ендпоінтом `access`

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** Viacheslav Melnichenko

## Context

Роль редактора видається серверною конфігурацією (`Identity:ContentEditorTelegramUserIds` у секретах) і додається `ContentEditorClaimsTransformation` при кожному запиті. Mini App не знав про цю роль, тож у інтерфейсі не було входу до `/editor` ([US-06](../PRD.md#4-user-stories)). Потрібен спосіб показати рядок «Редактор словника» лише редакторам.

## Decision drivers

- Не розкривати Telegram id чи список редакторів ([PRD §6.1](../PRD.md#61-security--privacy)).
- Не змінювати схему `Learner`, яку повертають профіль, онбординг і налаштування.
- Без зайвих 403 у мережевому журналі для звичайних користувачів.
- Роль не вимагає завершеного онбордингу.

## Considered options

1. **Поле `is_editor` у `Learner`** — один запит, але `LearnerResponse` будують у трьох місцях без доступу до claims, і поле стало б обов'язковим для всіх відповідей і фікстур.
2. **Пробувати `GET /editor/content-reports` і дивитися на 403** — без нових ендпоінтів, але вантажить чергу заради перевірки ролі й дає 403 кожному звичайному користувачу.
3. **Окремий `GET /api/v1/learners/me/access`** → `{ "content_editor": boolean }`, завжди 200 для автентифікованого.

## Decision outcome

**Chosen:** Option 3. Контролер `LearnerAccessController` читає роль із claims (`User.IsInRole`) і не залежить від онбордингу. Контракт: версія 0.5.0, операція `getLearnerAccess`, схема `LearnerAccess` ([openapi.yaml](../../personal-trainer-mvp/contracts/openapi.yaml)). Mini App запитує його лише на екрані «Налаштування» й показує рядок, якщо `content_editor` дорівнює `true`.

## Consequences

**Positive**
- Нова відповідь не зачіпає `Learner`; роль не вимагає профілю; без шуму 403.
- Мінімальна й безпечна поверхня: лише булеве значення власної ролі.

**Negative / trade-offs**
- Один додатковий запит на відкриття «Налаштувань».
- Приховання рядка не є захистом: доступ до редакторських даних і далі перевіряє політика `ContentEditor` на сервері.
- Після зміни списку редакторів у секретах потрібен перезапуск пода: конфігурація Infisical читається лише на старті ([дизайн-специфікація §7](../design-spec.md#7-вхід-до-редактора)).

## Related

[PRD](../PRD.md) · [sync-звіт контракту](../../personal-trainer-mvp/contracts/api-sync-report.md) · [ADR-0005 personal-trainer-mvp](../../personal-trainer-mvp/adr/0005-authenticate-every-api-request-with-telegram-init-data.md)
