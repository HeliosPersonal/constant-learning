# Epic — unlimited-learning

> **PRD:** [PRD.md](../PRD.md) · **Design:** [sad.md](../sad.md) · **API:** [openapi.yaml](../contracts/openapi.yaml) · **Data:** [data-model.md](../data-model.md) · **ADRs:** [adr/](../adr/)

## Goal

Дати learner після виконання плану дня «Ще одне слово» і «Потренувати вивчені», зберігши ліміт як план на день, чесний статус слів і незмінні лічильники занять ([idea-brief §13](../idea-brief.md#13-recommendation), [PRD §2](../PRD.md#2-goals)).

## Scope

- **In:** модуль `Learning` у `ConstantLearning/` (сесії, планувальник, екран «Сьогодні»), міграція колонки `kind`, екрани «Сьогодні» й заняття в Mini App, тести, документація.
- **Out:** усе з [PRD §3](../PRD.md#3-non-goals) — список вивчених слів (окрема фіча), вибір слів, «слабкі слова», нагадування й досягнення, зміна ліміту.

## Task map

Той самий DAG, що й у [tasks.json](./tasks.json). Стрілка веде від залежності до задачі, яку вона розблоковує. T01 і T03 стартують паралельно; міграційні задачі T01 і T02 розгортаються окремими деплоями.

```mermaid
flowchart LR
    T01["T01 session-kind-expand"]
    T02["T02 session-kind-contract"]
    T03["T03 scheduler-extra-practice"]
    T04["T04 plan-kind-filters"]
    T05["T05 extra-availability-today"]
    T06["T06 start-extra-word"]
    T07["T07 start-extra-practice"]
    T08["T08 answer-pipeline-extra"]
    T09["T09 extra-endpoints-contract"]
    T10["T10 ui-today-and-session"]
    T11["T11 extra-integration-tests"]
    T12["T12 extra-load-check"]
    T13["T13 docs-update"]
    T01 --> T02
    T01 --> T04
    T01 --> T05
    T04 --> T05
    T01 --> T06
    T04 --> T06
    T03 --> T07
    T05 --> T07
    T03 --> T08
    T06 --> T08
    T07 --> T08
    T05 --> T09
    T06 --> T09
    T07 --> T09
    T09 --> T10
    T08 --> T11
    T09 --> T11
    T09 --> T12
    T10 --> T13
    T11 --> T13
```

## Coverage

Усі 20 AC з [PRD §5](../PRD.md#5-acceptance-criteria) покриті щонайменше однією задачею; тест-план — у розділі [Test plan](../PRD.md#test-plan).
