# Інфраструктура тестів

Реалізація [T02](../../docs/features/personal-trainer-mvp/tasks/T02-test-harness.md) за [планом тестування](../../docs/features/personal-trainer-mvp/test-plan.md) і [описом фікстур](../../docs/features/personal-trainer-mvp/data-model.md#test-fixtures).

Для локального запуску потрібен Docker із Linux-контейнерами. Testcontainers сам знаходить daemon, завантажує `postgres:16-alpine`, вибирає вільний порт і видаляє контейнер після набору тестів. Рядок підключення створюється лише для ефемерної БД.

```powershell
dotnet test ConstantLearning.Tests/ConstantLearning.Tests.csproj
dotnet test ConstantLearning.Tests/ConstantLearning.Tests.csproj --filter 'FullyQualifiedName~Infrastructure|FullyQualifiedName~Fixtures'
```

Інтеграційний клас наслідує `PostgresIntegrationTest`, приймає `PostgresFixture` у конструкторі й позначає тести `[DockerFact]`. Колекція PostgreSQL виконується послідовно й використовує один контейнер. Фікстура застосовує всі актуальні EF-міграції; перед кожним тестом база очищується через `TRUNCATE ... RESTART IDENTITY CASCADE`. Історія міграцій і довідкові знімки `morph_forms` / `reference_entries` зберігаються. `Db`, `Services` і `Clock` доступні похідним тестам. Кожен тест має окремий `FakeClock`, зареєстрований також як `IClock` через звичайний `AddPlatform`.

Недоступний Docker локально означає пропуск `[DockerFact]` із поясненням. У середовищах із `CI`, `GITHUB_ACTIONS` або `TF_BUILD` зі значенням, відмінним від порожнього, `false` і `0`, та сама ситуація завершується помилкою фікстури; пропуск не встановлюється. Помилки завантаження образу, запуску контейнера чи застосування міграцій завжди спричиняють падіння. Політику перевіряють `DockerAvailabilityTests`; її не слід обходити фільтром тестів у CI.

Фабрики `LearnerFixtures`, `ContentFixtures` і `LearningFixtures` поки повертають тестові DTO, адже сутності нової схеми ще не перенесено. Наступні задачі адаптують їх до сутностей. Ідентифікатори — UUID v7, Telegram id починаються з `900000001`, ім’я — `Test User`; час за замовчуванням зафіксований на `2026-10-31T09:00:00Z`. Переданий `IClock` дозволяє задати інший момент. `NewActiveSession` створює початкову session за поточним поясом; сценарії зміни поясу задають доменні тести окремо.

`TelegramInitDataSigner` формує URL-кодовані `initData` і заголовок `tma` за [рішенням про автентифікацію](../../docs/features/personal-trainer-mvp/adr/0005-authenticate-every-api-request-with-telegram-init-data.md). Для негативних сценаріїв можна передати інший тестовий токен, старий `authDate` або змінити повернений рядок. Підпис перевірено незалежно обчисленим HMAC-вектором; мережеві виклики Telegram не потрібні.

`ContractAssert.ResponseAsync(operationId, response)` перевіряє статус, `Content-Type` і JSON за [OpenAPI](../../docs/features/personal-trainer-mvp/contracts/openapi.yaml). `ContractAssert.Response` приймає статус і JSON без HTTP-клієнта. Перевірка використовує JSON Schema 2020-12, розв’язує `$ref`, перевіряє nullable-поля, формати, обов’язкові поля й обмеження схеми.

`ContractAssert.EventRow(channel, json)` читає payload-таблиці безпосередньо з [контракту подій](../../docs/features/personal-trainer-mvp/contracts/events.md) і перевіряє обов’язкові поля, типи, формати та переліки значень. Додаткові колонки рядка БД дозволені, адже payload описує лише частину колонок. Унікальність, транзакції, повторні спроби й переходи станів перевірятимуть integration-тести відповідних реалізацій після [перенесення таблиць черг](../../docs/features/personal-trainer-mvp/tasks/T07-migrate-search-jobs-dispatches.md).
