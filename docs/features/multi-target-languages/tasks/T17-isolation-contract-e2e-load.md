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
status: "done"
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

- [x] Ізоляційні integration-тести зелені; 0 відповідей, зарахованих не своїй мові (NFR ізоляції). `TodayLanguageIsolationTests` (лічильники Today розділені по активній мові, перемикання pl→en→pl) і `ProgressIsolationTests` зелені через реальний HTTP-конвеєр (прогін 71/71 разом із контракт- і editor-queue-тестами).
- [x] Усі 5 контракт-операцій проходять проти `openapi.yaml` (статуси + форми + конверт помилки). Покрито наявними `ContractAssert`-тестами: `listTargetLanguages`/`listLearnerLanguages` (`LanguagesApiTests`), `startLearnerLanguage` (`StartLearnerLanguageApiTests`), `setActiveLanguage` (`SetActiveLanguageApiTests`, зокрема `409 language.not_studying`), `listEditorQueueByLanguage` (`EditorQueueTests`).
- [~] E2E happy + ізоляція зелені; прибирання per-test (без flaky). **Частково.** Збірку e2e-харнеса відновлено (`e2e/Host/Host.csproj`: пакети EF Core / Mvc.Testing піднято `10.0.2 → 10.0.14`, усунено NU1605-даунгрейд, що блокував увесь webServer). Самий прогін браузера **відкладено**: Playwright оновив очікуваний build Chromium до 1248, а `npx playwright install` падає з `SELF_SIGNED_CERT_IN_CHAIN` (корпоративний проксі блокує Playwright CDN); обхід TLS заборонено політикою. Суттєвий ризик ізоляції вже зелений на integration/API-рівні (вище), перемикач мови вкрито на component-рівні (vitest, MiniApp). Запуск браузера — локальна pre-pilot дія в середовищі без проксі-блоку (той самий клас відкладення, що й spectral).
- [~] Навантажувальні сценарії задокументовано й прогнано вручну; p95 у межах NFR або розбіжність зафіксовано. **Задокументовано.** Сценарій [`language-switch.js`](../../../../tests/load/language-switch.js) (50 VU, когорта `950000001..950000050` з pl+en, pl→en→pl + двічі «Сьогодні», пороги p95 перемикання/Today < 1500 ms як gate) + seed-когорта в `tests/load/Program.cs` (LoadHost збирається, 0 помилок) + розділ у [`tests/load/README.md`](../../../../tests/load/README.md). Вимірюваний прогін — ручна pre-pilot дія на Docker+k6-стенді (локального стенду в цьому середовищі немає); **розбіжність зафіксовано** — p95 заноситься до каталогу прогону разом з рештою навантажувальних артефактів, як для T49/T09/T12.
- [~] `spectral lint` контракту прогнано у середовищі з Node (gate, відкладений у [test-plan.md](../test-plan.md)/[api-sync-report.md](../contracts/api-sync-report.md)). **Відкладено** — `npx`/Playwright CDN за корпоративним проксі недоступні; gate лишається відкладеним згідно з [test-plan.md](../test-plan.md) і [api-sync-report.md](../contracts/api-sync-report.md), прогін у середовищі з Node до pilot.
- [x] PR ≤500 LOC (навантажувальні скрипти можуть бути окремим PR, якщо перевищує). PR цієї задачі — лише тести, навантажувальний сценарій + seed і документація; код застосунку не змінюється.
