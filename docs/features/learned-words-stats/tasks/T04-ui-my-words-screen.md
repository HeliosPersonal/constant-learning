---
id: T04
title: "Побудувати екран «Мої слова» в Mini App: вкладки, пошук, сортування, лічильники й картка з розбивкою помилок"
layer: "ui"
deps: ["[T03](./T03-stats-controller-and-request.md)"]
acs: ["AC-01", "AC-04", "AC-05", "AC-09", "AC-10"]
files_hint: ["ConstantLearning.MiniApp/src/features/stats/", "ConstantLearning.MiniApp/src/api/"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "done"
---

# T04 — Побудувати екран «Мої слова» в Mini App: вкладки, пошук, сортування, лічильники й картка з розбивкою помилок

**Шар:** `ui` · **Залежить від:** [T03](./T03-stats-controller-and-request.md) · **AC:** [AC-01](../PRD.md#ac-01---domain-invariant-усі-показує-лише-побачені-слова), [AC-04](../PRD.md#ac-04---domain-invariant-вкладки-й-зведення-узгоджені-зі-сьогодні), [AC-05](../PRD.md#ac-05---happy-path-тренування-слабких-слів), [AC-09](../PRD.md#ac-09---happy-path-сортування), [AC-10](../PRD.md#ac-10---happy-path-пошук) · **Оцінка:** M

## Why

[idea-brief §7 Approach C](../idea-brief.md#7-strategic-approaches); дизайн — наявні `Card`, `Button`, `Tabs` та токени Everforest з [design-system](../../personal-trainer-mvp/design-system.md).

## What

Екран «Мої слова» з вкладками «Усі» / «На повторення» / «Вже пригадую» (лічильники зі зведення), пошуком і сортуванням (recent / alpha / weakest), рядком слова з лічильниками (показ / правильно / неправильно / серія / забуте) і карткою слова з перекладами й розбивкою помилок (друкарська / діакритична / «не знаю»). Дія «тренувати слабкі» запускає наявний `POST /api/v1/sessions/extra-practice` — окремого ендпоінта тренування немає. Порожній стан для нового учня без слів; підвантаження порціями курсором. Оновити типи API-клієнта з контракту.

Файли: `ConstantLearning.MiniApp/src/features/stats/`, `ConstantLearning.MiniApp/src/api/`

## Definition of Done

- [ ] компонентні тести AC-01, AC-04, AC-09, AC-10 зелені (вкладки, пошук, сортування, порожній стан)
- [ ] «тренувати слабкі» викликає POST /api/v1/sessions/extra-practice і відкриває заняття (AC-05)
- [ ] стани «Пригадую» й «На повторення» пояснено на екрані
- [ ] збірка й лінтер без нових попереджень
