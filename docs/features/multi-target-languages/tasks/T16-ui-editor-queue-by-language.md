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
status: "done"
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

- [x] Черга показує повідомлення лише обраної мови; перемикання мови змінює набір (AC-11) — фільтр `Мова черги` над чергою передає `target_language` у `listEditorQueueByLanguage`, порожнє значення «Усі мови» = без фільтра; перемикання скидає курсор.
- [x] Схвалення/відкат з UI зачіпає словник правильної мови (через T14) — елемент черги несе `target_language` + нейтральну `lemma`; рішення/відкат проходять беззмінним бекендом T14, що ізолює словник за мовою повідомлення.
- [x] Курсорна пагінація працює (наступна сторінка, відсутність offset) — `listQueue` передає `after/before/limit`, зміна мови очищає `cursors.queue`.
- [x] Локальні перевірки (lint/build/typecheck) зелені — `tsc --noEmit` чисто; `eslint .` 0 помилок (1 попередження, не пов'язане, у `SessionRoute.tsx`); `vitest run` 281/281.
- [x] PR ≤500 LOC — дельта контракту/SPA/тестів у межах.
