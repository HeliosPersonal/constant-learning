---
id: T17
title: "Наскрізна ізоляція / контракт / e2e / навантаження"
layer: "tests"
deps: ["[T02](./T02-test-fixtures.md)", "[T13](./T13-set-active-scope-existing.md)", "[T14](./T14-editor-queue-by-language.md)", "[T15](./T15-ui-language-switcher.md)", "[T16](./T16-ui-editor-queue-by-language.md)"]
acs: ["AC-04", "AC-05", "AC-10", "AC-11", "AC-12"]
files_hint:
  - "ConstantLearning.Tests/Integration/"
  - "ConstantLearning.Tests/Contract/"
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "todo"
---

# T17 — Наскрізна ізоляція / контракт / e2e / навантаження

**Шар:** tests · **Залежить від:** [T02](./T02-test-fixtures.md), [T13](./T13-set-active-scope-existing.md), [T14](./T14-editor-queue-by-language.md), [T15](./T15-ui-language-switcher.md), [T16](./T16-ui-editor-queue-by-language.md) · **AC:** AC-04, AC-05, AC-10, AC-11, AC-12 · **Оцінка:** L · **Фаза:** 7 (Верифікація)

## Why

[test-plan.md](../test-plan.md) робить ізоляцію ризиком №1 і вимагає наскрізної перевірки, якої
окремі задачі не дають: крос-learner + крос-мовний прогрес, контракт усіх 5 операцій, e2e критичних
flow і навантажувальні сценарії під NFR ([PRD §6](../PRD.md#6-non-functional-requirements)).

## What

- **Ізоляційний блок** ([test-plan.md §Ізоляція](../test-plan.md)): EN-відповідь не рахується в PL;
  Today-лічильники виключають інші мови; черга A не містить елементів B; крос-learner приховано (AC-10);
  інваріант «мова рядка = мова entry».
- **Контракт-тести**: 5 операцій (`listTargetLanguages`, `listLearnerLanguages`, `startLearnerLanguage`,
  `setActiveLanguage`, `listEditorQueueByLanguage`) проти [openapi.yaml](../contracts/openapi.yaml) — коди й форми.
- **E2E через UI**: обрати мову → заняття → перемкнути → переконатися, що облік роздільний (AC-04/AC-12).
- **Навантаження (NFR)**: p95 перемикання ≤1500 ms, Today ≤1500 ms, відповідь ≤500 ms, пошук ≤300 ms,
  ізоляція = 0 — сценарії запускаються вручну pre-pilot ([test-plan.md §NFR](../test-plan.md)).

## Definition of Done

- [ ] Ізоляційні integration-тести зелені; 0 відповідей, зарахованих не своїй мові (NFR ізоляції).
- [ ] Усі 5 контракт-операцій проходять проти `openapi.yaml` (статуси + форми + конверт помилки).
- [ ] E2E happy + ізоляція зелені; прибирання per-test (без flaky).
- [ ] Навантажувальні сценарії задокументовано й прогнано вручну; p95 у межах NFR або розбіжність зафіксовано.
- [ ] `spectral lint` контракту прогнано у середовищі з Node (gate, відкладений у [test-plan.md](../test-plan.md)/[api-sync-report.md](../contracts/api-sync-report.md)).
- [ ] PR ≤500 LOC (навантажувальні скрипти можуть бути окремим PR, якщо перевищує).
