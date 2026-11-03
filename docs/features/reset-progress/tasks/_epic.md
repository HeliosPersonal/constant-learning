# Epic — reset-progress

> **PRD:** [PRD.md](../PRD.md) · **Design:** [sad.md](../sad.md) · **API:** [openapi.yaml](../contracts/openapi.yaml) · **ADRs:** [adr/](../adr/)

## Goal

Дати learner кнопку в налаштуваннях, що незворотно стирає весь його прогрес навчання й лишає налаштування та content reports ([idea-brief](../idea-brief.md)).

## Scope

- **In:** модулі `Identity`, `Learning`, `Content` у `ConstantLearning/`, екран налаштувань і діалог у Mini App, тести, документація.
- **Out:** усе з [PRD §3](../PRD.md#3-non-goals) — часткове скидання, архів і відновлення, експорт історії. Нових міграцій немає.

## Task map

Той самий DAG, що й у [tasks.json](./tasks.json). Стрілка веде від залежності до задачі, яку вона розблоковує.

```mermaid
flowchart LR
    T01["T01 reset-flow-and-port"]
    T02["T02 learning-reset-handler"]
    T03["T03 content-reset-handler"]
    T04["T04 reset-endpoint"]
    T05["T05 ui-reset-progress"]
    T06["T06 reset-integration-tests"]
    T07["T07 reset-completeness-test"]
    T08["T08 reset-telemetry-counter"]
    T09["T09 reset-load-check"]
    T10["T10 docs-update"]
    T01 --> T02
    T03 --> T02
    T01 --> T03
    T01 --> T04
    T02 --> T04
    T03 --> T04
    T04 --> T05
    T04 --> T06
    T02 --> T07
    T03 --> T07
    T04 --> T08
    T04 --> T09
    T05 --> T10
    T06 --> T10
    T08 --> T10
```

Після T01 іде T03, потім T02 (порядок обробників: спершу Content); після T04 — T05, T06, T08, T09.

## Coverage

Усі 12 AC з [PRD §5](../PRD.md#5-acceptance-criteria) покриті щонайменше однією задачею; тест-план — у розділі [Test plan](../PRD.md#test-plan).
