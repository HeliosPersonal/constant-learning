---
id: T02
title: "Реалізувати LearnedWordsStatsService: лічильники наживо з журналу з анти-join оскаржень і показ з опублікованої версії"
layer: "app"
deps: []
acs: ["AC-02", "AC-03", "AC-08", "AC-12"]
files_hint: ["ConstantLearning/Features/Learning/Stats/LearnedWordsStatsService.cs", "ConstantLearning/Features/Learning/Stats/StatsResponses.cs", "ConstantLearning.Tests/Learning/Stats/"]
owner: "Viacheslav Melnichenko"
estimate: "L"
status: "done"
---

# T02 — Реалізувати LearnedWordsStatsService: лічильники наживо з журналу з анти-join оскаржень і показ з опублікованої версії

**Шар:** `app` · **Залежить від:** — · **AC:** [AC-02](../PRD.md#ac-02---domain-invariant-визначення-лічильників), [AC-03](../PRD.md#ac-03---domain-invariant-скасоване-оскарження-не-рахується-помилкою), [AC-08](../PRD.md#ac-08---domain-invariant-слабке-слово), [AC-12](../PRD.md#ac-12---cross-context-поля-з-опублікованої-версії) · **Оцінка:** L

## Why

[idea-brief §10](../idea-brief.md#10-risks) (найкритичніший ризик — лічильники мають збігатися з оцінкою тренера), [openapi](../contracts/openapi.yaml) (описи полів LearnedWordStat).

## What

Сервіс завантажує `learner_words` зі статусом `review` або `recalled` (лише побачені слова) і рахує лічильники наживо в пам'яті за запит з `answer_events`: `shown` — зараховані спроби видів `review`, `control_repetition`, `extra_practice` (нейтральні та неоцінювані картки не входять); `correct` — спроби з `outcome = correct`; `incorrect` — помилки за винятком успішно оскаржених через анти-join із `answer_neutralizations`; `mistake_breakdown` — `typo` / `diacritic` / `dont_know` з тим самим вирахуванням оскарженого одруку. `streak` береться з `consecutive_scheduled_correct`, `forgotten_count` — з `lapses`, `is_weak` = status != recalled і (lapses > 0 або incorrect > 0). Показ (лема, транскрипція, переклади) бере з опублікованої `EntryVersion` (за відсутності вказівника — остання версія за `created_at`): `transcription_uk` = null, якщо запис втратив публікацію; канонічний переклад іде першим — так само, як на «Сьогодні».

Файли: `ConstantLearning/Features/Learning/Stats/LearnedWordsStatsService.cs`, `ConstantLearning/Features/Learning/Stats/StatsResponses.cs`, `ConstantLearning.Tests/Learning/Stats/`

## Definition of Done

- [ ] інтеграційні тести AC-02, AC-03, AC-08, AC-12 зелені на тимчасовій БД
- [ ] успішно оскаржений одрук і нейтральна відповідь-синонім не рахуються ні помилкою, ні успіхом
- [ ] одноразова перевірка нового слова не роздуває лічильники; recalled-слово ніколи не слабке
- [ ] `dotnet build` і тести без нових попереджень
