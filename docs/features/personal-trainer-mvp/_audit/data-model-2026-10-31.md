# Аудит моделі даних — personal-trainer-mvp — 2026-10-31

## Згенеровані файли

- [`data-model.md`](../data-model.md) — ER, 19 таблиць, обґрунтування індексів.
- 22 пари staged-міграцій у [`migrations/`](../migrations/), від `20261031120000_create_learners` до `20261031120021_drop_group_model`.
- `.claude/rules/migrations.md` — створено з базового шаблону курсу (раніше не існував).

**Міграції staged:** їх ще немає в живому дереві `ConstantLearning/Migrations/`. `implement-tasks` переносить кожну пару з новою позначкою часу, зберігаючи порядок усередині фічі.

## Розходження з конвенціями репозиторію

| Тема | Репозиторій | Ця фіча (дефолт курсу) | Що робити під час реалізації |
|---|---|---|---|
| Механізм міграцій | EF Core Migrations у C#, автоматичне застосування на старті (`HostedServices/DatabaseInitializationService.cs:21`) | SQL-first `.up.sql` / `.down.sql` | Кожну пару загорнути в EF-міграцію через `migrationBuilder.Sql(...)` в `Up`/`Down`; модель EF налаштувати так, щоб snapshot збігався з DDL. Розбіжність snapshot і SQL — ризик дрейфу; варто розглянути `/sdlc:decide-adr` |
| Первинні ключі | `int` identity (`Migrations/20260130060918_Init.cs:20`); [SAD §8](../sad.md#8-crosscutting-concepts) спершу зафіксував так само | UUID v7, генерується застосунком (`Guid.CreateVersion7()`) | [SAD §8](../sad.md#8-crosscutting-concepts) оновлено. Старі таблиці з `int` видаляються ([ADR-0004](../adr/0004-retire-the-group-model-without-data-migration.md)), тож змішаних ключів не буде |
| Назви таблиць | PascalCase у лапках від EF (`"ChatRegistrations"`) | `snake_case` у множині | Налаштувати EF-мапінг (`ToTable`/`HasColumnName` або іменувальну конвенцію Npgsql) |
| Імена файлів міграцій | `yyyyMMddHHmmss_Name.cs` | `yyyyMMddHHmmss_<verb>_<entity>.up.sql` | Позначка часу збігається; назву зберегти в імені EF-міграції |
| `updated_at` | Не використовується | Не використовується | Розбіжності немає; змінювані таблиці (налаштування learner, проєкції, статуси) свідомо без `updated_at` — історія змін PRD не потрібна |

## Дрейф

- **Схема проти коду:** доменного шару нової моделі ще немає, тож перевірку пропущено. `_drift/` не створювався.
- **Модель проти специфікації:**
  - Усі сутності з [PRD §4](../PRD.md#4-user-stories)–§5 і SAD мають таблиці: learner, нагадування, слова learner, session, картки, журнал відповідей, нейтралізації, записи й версії словника, переклади, прийняті відповіді, докази, журнал публікацій, content reports, задачі звірки, форми пошуку, лексикон, довідкові знімки, відправлення.
  - SAD не має розділу §6.4 ER (шаблон Arc42 його не містить), тому зв'язки взято з §5, §6, [ADR-0007](../adr/0007-dispatch-notifications-through-postgres-dispatch-table.md)–0012.
  - Таблиць без походження з PRD/SAD немає.
  - Відкрите питання [SAD §11](../sad.md#11-risks-and-technical-debt) «поле прийнятих польських варіантів відсутнє» закрито на рівні схеми таблицею `version_accepted_answers`; `docs/dictionary/content-schema.md` і редакторський CSV ще треба доповнити.
  - Клієнтська телеметрія «Сьогодні» ([SAD §7](../sad.md#7-deployment-view)) таблиці не має — свідомо: метрика йде в логи або систему метрик, не в БД.

## Breaking changes

- `20261031120021_drop_group_model` — contract-фаза [ADR-0004](../adr/0004-retire-the-group-model-without-data-migration.md). Застосовувати **окремим релізом** після видалення групового коду й після `pg_dump`. `.down.sql` відновлює лише структуру; дані — з дампу.

## Самоперевірка

| Перевірка | Результат |
|---|---|
| Назви таблиць і файлів | ✅ `snake_case` у множині, `<timestamp>_<verb>_<entity>` |
| Оборотність `.down.sql` | ✅ кожна таблиця, індекс і обмеження з `.up.sql` прибирається в `.down.sql` |
| Індекси FK | ✅ 32 зовнішні ключі, у кожного є індекс із цією колонкою на початку |
| Заборонені конструкції | ✅ немає `CHECK`, `TRIGGER`, бізнесових `DEFAULT` |
| ER-діаграма | ⚠️ структурний лінт пройдено; парсером Mermaid не перевірено (`npx` заблоковано) |

## TBD

- Немає `<!-- TBD -->` у `data-model.md`.
- Ліцензія `morph_forms` для української (VESUM) — [SAD §11](../sad.md#11-risks-and-technical-debt), вирішити до наповнення таблиці.

## Синхронізація після другого clarify-prd (2026-10-31)

- Додано пару `20261031120022_alter_content_reports_dedup_and_summary`; тепер міграцій 23 пари, таблиць так само 19.
- `content_reports.dedup_key` + частковий UNIQUE для незакритих reports — повтор того самого повідомлення не створює нового (AC-51).
- UNIQUE на `content_reports.answer_event_id` замість FK-індексу — один report на оцінену відповідь (AC-47).
- `content_reports.summary_dispatch_id` і частковий індекс замість `ix_content_reports_learner_closed` — підсумок «з моменту попереднього» (AC-37).
- `ix_publication_log_actor_created` — список застосованих змін для відкату (US-12, AC-48).
- Індекси створюються без `CONCURRENTLY`: таблиці нові в цій фічі й порожні на момент застосування. Самоперевірка нової пари: назва ✅, `.down.sql` повертає попередні індекси й прибирає колонки ✅, FK-індекс для `summary_dispatch_id` ✅, без `CHECK`/`TRIGGER`/бізнесових `DEFAULT` ✅.
