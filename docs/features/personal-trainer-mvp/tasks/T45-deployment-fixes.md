---
id: T45
title: "Оновити збірку й маніфести розгортання"
layer: "wiring"
deps: ["[T34](T34-miniapp-skeleton.md)"]
acs: []
files_hint: ["ConstantLearning/Dockerfile", "k8s/", ".github/workflows/"]
owner: "Viacheslav Melnichenko"
estimate: "S"
status: "todo"
---

# T45 — Оновити збірку й маніфести розгортання

**Шар:** `wiring` · **Залежить від:** [T34](./T34-miniapp-skeleton.md) · **AC:** — (технічна передумова) · **Оцінка:** S · **Фаза:** 3

## Why

Обмеження з [architecture-map.md](../../../architecture-map.md) і ризики [sad §11](../sad.md#11-risks-and-technical-debt).

## What

Dockerfile збирає Mini App у `wwwroot`; probes на правильний маршрут health; змінна рядка підключення збігається з кодом; перевірка ICU і tzdata в образі; CI запускає тести.

Для пілоту використовуються нічні зашифровані резервні копії поза кластером зі строком зберігання 14 днів. Перед [T44](T44-migrate-drop-group-model.md) копію відновлюють в окрему БД; доступність перевіряє зовнішній сервіс раз на хвилину зі сповіщенням після трьох невдалих перевірок поспіль. Ручний процес приймання й виконання запиту на видалення learner описується в інструкції запуску.

Файли: `ConstantLearning/Dockerfile`, `k8s/`, `.github/workflows/`

## Definition of Done

- [ ] образ збирається локально й відповідає на probe
- [ ] у контейнері пояси Europe/Warsaw і Asia/Tokyo розв'язуються
- [ ] `dotnet build`, тести й лінтер без нових попереджень
- [ ] інструкція пілоту містить розклад і строк зберігання копій, крок пробного відновлення перед [T44](T44-migrate-drop-group-model.md), процедуру видалення learner протягом 7 календарних днів і опис зовнішньої перевірки доступності
