# Constant Learning

Персональний тренер польської мови в Telegram: бот нагадує про заняття, а Mini App проводить урок, показує словник і налаштування. Бекенд на .NET 10 і PostgreSQL, фронтенд — React-застосунок, який ASP.NET Core роздає як SPA.

Груповий режим більше не підтримується. Старий код груп ще є в репозиторії й прибирається в [T43](docs/features/personal-trainer-mvp/tasks/T43-retire-group-code.md).

## Як це працює

~~~mermaid
flowchart LR
    U[Учень] -->|чат з ботом| TG[Telegram Bot API]
    U -->|відкриває Mini App| APP
    TG -->|webhook: POST /api/telegram/webhook| APP[ASP.NET Core: бот, API і SPA]
    APP -->|повідомлення і нагадування| TG
    APP -->|EF Core| DB[(PostgreSQL)]
~~~

Mini App викликає API з підписом Telegram `initData` ([ADR-0005](docs/features/personal-trainer-mvp/adr/0005-authenticate-every-api-request-with-telegram-init-data.md)). Бот відповідає на повідомлення та надсилає нагадування з посиланням на Mini App. Заняття, відповіді й розклад повторень зберігаються на сервері ([ADR-0008](docs/features/personal-trainer-mvp/adr/0008-keep-the-learning-session-server-side-with-per-card-idempotency.md)).

## Швидкий запуск

Потрібні Docker з Compose і токен бота від [@BotFather](https://t.me/BotFather).

1. Задайте змінні середовища або локальний файл для Compose:

   ~~~text
   TELEGRAM_BOT_TOKEN=<токен від BotFather>
   TELEGRAM_WEBHOOK_URL=   # порожньо для локального запуску без webhook
   ~~~

2. Запустіть сервіси:

   ~~~sh
   docker compose up -d
   docker compose logs -f app
   ~~~

3. Відкрийте чат із ботом і надішліть `/start`.

Без `TELEGRAM_WEBHOOK_URL` бот не отримуватиме повідомлення від Telegram. Щоб тестувати webhook локально, використайте ngrok (нижче).

## Локальна розробка

Бекенд:

~~~sh
dotnet restore ConstantLearning.slnx
dotnet build ConstantLearning.slnx
dotnet run --project ConstantLearning
~~~

Mini App (з каталогу `ConstantLearning.MiniApp`):

~~~sh
npm ci
npm run dev
npm run build
~~~

`npm run dev` відкриває `http://127.0.0.1:5173`, а `/api` проксіюється на `http://127.0.0.1:8888`. `npm run build` записує готову SPA в `ConstantLearning/wwwroot`. Деталі — в [README Mini App](ConstantLearning.MiniApp/README.md).

Тести бекенду потребують Docker: Testcontainers піднімає PostgreSQL 16.

~~~sh
dotnet test ConstantLearning.Tests/ConstantLearning.Tests.csproj
~~~

Міграції EF Core:

~~~sh
dotnet ef migrations add НазваМіграції
dotnet ef database update
~~~

## Webhook через ngrok

Telegram приймає webhook лише через HTTPS. Для локального тесту:

1. Запустіть тунель на порт застосунку: `ngrok http 8888`.
2. Задайте `TELEGRAM_WEBHOOK_URL` як HTTPS-адресу ngrok із шляхом `/api/telegram/webhook`.
3. Перезапустіть застосунок: `docker compose restart app`.

Безкоштовна адреса ngrok змінюється після перезапуску тунелю, тоді оновіть `TELEGRAM_WEBHOOK_URL` і перезапустіть застосунок.

~~~mermaid
sequenceDiagram
    participant A as Застосунок
    participant T as Telegram Bot API
    participant U as Учень
    A->>T: Реєстрація webhook при старті
    U->>T: Повідомлення боту
    T->>A: POST /api/telegram/webhook
    A-->>T: HTTP 200
    Note over A,T: При зупинці застосунку webhook знімається
~~~

Перевірка через Bot API: `https://api.telegram.org/bot<TOKEN>/getWebhookInfo`. У полі `url` має бути ваша адреса, а `last_error_message` покаже причину збоїв.

Типові причини:

- **Адреса не HTTPS.** Telegram вимагає HTTPS.
- **Неправильний шлях.** Має закінчуватися на `/api/telegram/webhook`.
- **Адреса недоступна.** Перевірте тунель, DNS, TLS та брандмауер.
- **Застосунок не відповідає.** Перевірте, що контейнер слухає порт 8888 і в журналі є запити.

## Налаштування

| Ключ | Призначення |
|---|---|
| `Telegram__BotToken` | Токен бота (`TELEGRAM_BOT_TOKEN` у Compose). Обов'язковий. |
| `Telegram__WebhookUrl` | Публічна HTTPS-адреса webhook. Порожнє значення вимикає реєстрацію. |
| `Telegram__SecretToken` | Секрет webhook. У production задається в Infisical. |
| `Telegram__MiniAppUrl` | Публічна адреса Mini App. |
| `ConnectionStrings__DefaultConnection` | Рядок підключення до PostgreSQL. |
| `Language__TargetLanguage`, `Language__SourceLanguage` | Мовна пара: Polish і Ukrainian. |
| `Language__TargetLanguageCode`, `Language__SourceLanguageCode` | Коди мов: `pl` і `uk`. |
| `WordsImport__WordsFolder` | Каталог CSV зі словами для імпорту. |

Параметри `Learning__*` (кількість слів, затримка відповіді) діють лише для старих групових завдань і зникнуть разом із ними.

Production-конфігурація, секрети та порядок деплою — в [документі про деплой](docs/deployment.md).

## Структура репозиторію

~~~text
ConstantLearning/            Бекенд: webhook, API, сервіси, EF Core
├── Controllers/             Telegram webhook і health
├── Features/                Модулі функцій (див. ADR-0003)
├── Platform/                Спільні компоненти
├── Data/, Migrations/       Контекст БД і міграції EF Core
├── Services/                Логіка бота та навчання
├── HostedServices/          Старт: БД і webhook
├── Jobs/                    Старі групові завдання Quartz
├── Resources/               Тексти бота, шаблони, словники
└── wwwroot/                 Збірка Mini App
ConstantLearning.MiniApp/    Mini App (React, Vite)
ConstantLearning.Tests/      Тести .NET (xUnit, Testcontainers)
docs/                        Документація
k8s/, tools/, tests/         Маніфести, інструменти, навантажувальні тести
~~~

Модулі персонального тренера описано в [ADR-0003](docs/features/personal-trainer-mvp/adr/0003-organise-code-into-feature-modules-inside-the-monolith.md).

## Документація

| Документ | Про що |
|---|---|
| [Деплой](docs/deployment.md) | Автоматичний production через GitHub Actions, Helios і Infisical, rollback |
| [PRD персонального тренера](docs/features/personal-trainer-mvp/PRD.md) | Вимоги, критерії приймання, KPI |
| [Ідеї та рішення](docs/IDEAS.md) | Журнал рішень інтерв'ю |
| [Трекер задач](docs/features/personal-trainer-mvp/tasks/tracker.md) | Статус реалізації |
| [Словник](docs/dictionary/README.md) | Випуск 2 000 схвалених лем і метод перевірки |
| [Локальна LLM](docs/LOCAL-LLM.md) | Дослідження моделей; рішення в [ADR-0014](docs/features/personal-trainer-mvp/adr/0014-prepare-initial-dictionary-with-source-backed-llm-editing.md) |
| [Mini App](ConstantLearning.MiniApp/README.md) | Фронтенд, теми, локальні перевірки |
| [Інфраструктура тестів](ConstantLearning.Tests/Infrastructure/README.md) | Фікстури PostgreSQL і контрактні перевірки |
| [Глосарій](CONTEXT.md) | Терміни домену |
| [Правила для агентів](AGENTS.md) | Мова документації та авторство комітів |
