---
id: T45
title: "Оновити збірку й маніфести розгортання"
layer: "wiring"
deps: ["T34"]
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

Файли: `ConstantLearning/Dockerfile`, `k8s/`, `.github/workflows/`

## Definition of Done

- [ ] образ збирається локально й відповідає на probe
- [ ] у контейнері пояси Europe/Warsaw і Asia/Tokyo розв'язуються
- [ ] `dotnet build`, тести й лінтер без нових попереджень
