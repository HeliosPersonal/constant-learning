---
id: T07s
title: "Spike: джерела й ліцензії для en/de/nl + заведення профілів нормалізації (OQ-7)"
layer: "research"
deps: ["[T01](./T01-config-target-languages.md)"]
acs: ["AC-08"]
files_hint:
  - "docs/features/multi-target-languages/_audit/"
  - "ConstantLearning/Features/Content/Search/Forms/ (seed профілів)"
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T07s — Spike: джерела й ліцензії для en/de/nl + заведення профілів нормалізації

**Шар:** research · **Залежить від:** [T01](./T01-config-target-languages.md) · **AC:** AC-08 · **Оцінка:** S · **Фаза:** 3 (Content)

> **OQ-7 (відкрите питання).** Наявність і ліцензійність вільних per-language джерел
> (морфологічний лексикон, частотний корпус, довідкове джерело автозвірки) для en/de/nl
> ще не підтверджені. Цей spike — **окрема задача перед реалізацією** (не умовний крок усередині
> [T07a](./T07a-generalize-forms-pl-en.md)/[T07b](./T07b-full-rules-de-nl.md)), щоб обсяг
> дослідження не роздував PR реалізації. Див. [PRD §8 OQ-7](../PRD.md#8-open-questions),
> [SAD §11](../sad.md#11-risks-and-technical-debt).

## Why

Перш ніж кодувати мово-специфічні правила форм/нормалізації, треба зафіксувати **звідки** беруться
дані на кожну мову і **на яких ліцензіях** їх можна вживати у продукті. Без цього T07a/T07b
стартують із невідомим обсягом (ризик монстро-PR). Spike закриває невизначеність і заводить
профілі `normalization_profile` у конфіг ([ADR-0004](../adr/0004-model-supported-languages-as-config-table.md)).

## What

- Відібрати для **en/de/nl** вільно-ліцензовані джерела: морфологічний лексикон (напр. Wiktionary /
  Unimorph), частотний корпус, довідкове джерело автозвірки (CC-ліцензовані корпуси).
- Для кожного джерела зафіксувати: URL/версію, ліцензію, придатність до продукту, обсяг/покриття.
- Завести профілі `normalization_profile` `'en'`/`'de'`/`'nl'` у seed `target_languages` (значення
  профілю, не NULL) — без кодування самих правил (це T07a/T07b).
- Зафіксувати межу: мова без підтвердженого джерела лишається `is_enabled=false` (AC-02); увімкнення
  настає з наявністю approved-контенту (поза обсягом цієї задачі).

## Definition of Done

- [ ] Для en/de/nl задокументовані джерела + ліцензії в `_audit/` (одна таблиця: мова → джерело → ліцензія → покриття).
- [ ] Профілі `normalization_profile` `'en'`/`'de'`/`'nl'` заведені в seed `target_languages` (не NULL).
- [ ] Зафіксовано рішення OQ-7 у [SAD §11](../sad.md#11-risks-and-technical-debt) (із «Open question» → resolved) + [tracker.md](./tracker.md).
- [ ] [T07a](./T07a-generalize-forms-pl-en.md)/[T07b](./T07b-full-rules-de-nl.md) розблоковані: обсяг правил на мову відомий.
