---
id: T03
title: "Додати StatsController і LearnedWordsStatsRequest: розбір запиту, курсор за entry_id, валідація вкладки/сортування/пошуку й телеметрія"
layer: "app"
deps: ["[T01](./T01-stats-contract-and-errors.md)", "[T02](./T02-stats-service-counting.md)"]
acs: ["AC-01", "AC-09", "AC-10", "AC-11", "AC-13"]
files_hint: ["ConstantLearning/Features/Learning/Stats/StatsController.cs", "ConstantLearning/Features/Learning/Stats/LearnedWordsStatsRequest.cs", "ConstantLearning/Features/Learning/LearningModule.cs", "ConstantLearning/Platform/Metrics/TrainerMetrics.cs"]
owner: "Viacheslav Melnichenko"
estimate: "M"
status: "done"
---

# T03 — Додати StatsController і LearnedWordsStatsRequest: розбір запиту, курсор за entry_id, валідація вкладки/сортування/пошуку й телеметрія

**Шар:** `app` · **Залежить від:** [T01](./T01-stats-contract-and-errors.md), [T02](./T02-stats-service-counting.md) · **AC:** [AC-01](../PRD.md#ac-01---domain-invariant-усі-показує-лише-побачені-слова), [AC-09](../PRD.md#ac-09---happy-path-сортування), [AC-10](../PRD.md#ac-10---happy-path-пошук), [AC-11](../PRD.md#ac-11---happy-path-пагінація-курсором), [AC-13](../PRD.md#ac-13---error-невалідний-запит-і-неонбордений-learner) · **Оцінка:** M

## What

Контролер для трьох ендпоінтів за контрактом. `LearnedWordsStatsRequest` розбирає й валідує `tab` (all / recalled / review), `sort` (recent / alpha / weakest), `q` (1–200 символів, діакритика ігнорується) і курсор `after` / `before` за `entry_id` (непрозорий uuid, разом не допускаються, стабільний порядок між сторінками). Вкладка `all` повертає лише побачені слова й ніколи не розкриває порядок непоказаних слів курсу. Невалідні параметри (невідома вкладка/сортування, зайвий параметр, `after` і `before` разом, курсор не-uuid) дають `400 stats.invalid_request`; неонбордений learner — `404 learner.not_onboarded`; непідтверджена ідентичність — `401 auth.invalid_init_data`. Зареєструвати сервіс у `LearningModule` і додати телеметрію `RecordStatsQuery` у `TrainerMetrics`.

Файли: `ConstantLearning/Features/Learning/Stats/StatsController.cs`, `ConstantLearning/Features/Learning/Stats/LearnedWordsStatsRequest.cs`, `ConstantLearning/Features/Learning/LearningModule.cs`, `ConstantLearning/Platform/Metrics/TrainerMetrics.cs`

## Definition of Done

- [ ] інтеграційні тести AC-01, AC-09, AC-10, AC-11, AC-13 зелені на тимчасовій БД
- [ ] невалідні параметри дають stats.invalid_request, чужа/непідтверджена ідентичність не розкриває даних
- [ ] пагінація й сортування стабільні при однакових лічильниках
- [ ] `dotnet build` і тести без нових попереджень
