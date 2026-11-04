---
id: T16
title: "SPA: черга content reports редактора за мовою"
layer: "ui"
deps: ["[T14](./T14-editor-queue-by-language.md)"]
acs: ["AC-11"]
files_hint:
  - "ConstantLearning.MiniApp/src/features/editor/"
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "todo"
---

# T16 — SPA: черга content reports редактора за мовою

**Шар:** ui · **Залежить від:** [T14](./T14-editor-queue-by-language.md) · **AC:** AC-11 · **Оцінка:** M · **Фаза:** 6 (UI)

## Why

US-07/AC-11: content editor розглядає повідомлення, відокремлені за мовою, щоб мови не змішувалися
в черзі ([SAD §5 editor](../sad.md)). UI споживає `listEditorQueueByLanguage` (T14) з курсорною пагінацією.

## What

- Фільтр/вкладки за мовою у черзі редактора (`listEditorQueueByLanguage` з `target_language`).
- Кожен елемент черги несе мову; схвалення/відкат застосовується в межах мови повідомлення.
- Курсорна пагінація за контрактом (`items`, `has_next`, `next_cursor`).
- Telegram UI kit / узгоджена дизайн-система.

## Definition of Done

- [ ] Черга показує повідомлення лише обраної мови; перемикання мови змінює набір (AC-11).
- [ ] Схвалення/відкат з UI зачіпає словник правильної мови (через T14).
- [ ] Курсорна пагінація працює (наступна сторінка, відсутність offset).
- [ ] Локальні перевірки (lint/build/typecheck) зелені.
- [ ] PR ≤500 LOC.
