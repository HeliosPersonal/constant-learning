# Telegram-бот для вивчення мов

Бот надсилає групам короткі уроки польських слів і повторення за розкладом. Проєкт побудований на .NET 10 і PostgreSQL; вхідні оновлення Telegram надходять через webhook.

## Швидкий запуск

Потрібні Docker і Docker Compose, а також токен Telegram-бота від [@BotFather](https://t.me/BotFather).

1. Налаштуйте токен і рядок з’єднання в Docker Compose.
2. Запустіть сервіси:

~~~sh
docker compose up -d
~~~

3. Додайте бота до групи й надішліть команду /startlearning.

Докладні кроки наведено в [короткому посібнику](docs/QUICKSTART.md).

## Як працює бот

~~~mermaid
flowchart LR
    TG[Telegram: група] -->|webhook| API[ASP.NET Core бот]
    API --> SVC[Сервіси навчання]
    SVC --> DB[(PostgreSQL)]
    JOB[Заплановані завдання Quartz] --> SVC
    SVC -->|уроки та повторення| TG
~~~

Бот надсилає повторення вивчених слів і нові слова у встановлений для групи час. Стан навчання зберігається окремо для кожної групи.

## Можливості

- Незалежний прогрес і розклад для кожної групи.
- Адаптивний до мов налаштовуваний список слів; типова пара — польська й українська.
- Щоденні повідомлення з новими словами та повтореннями.
- Імпорт CSV-файлів зі словами.
- Команди для керування розкладом і прогресом.
- Розгортання через Docker.

## Команди

| Команда | Призначення |
|---|---|
| /startlearning | Зареєструвати групу й увімкнути навчання |
| /stoplearning | Призупинити заплановані повідомлення |
| /restartprogress | Очистити прогрес групи |
| /getalllearned | Показати вивчені слова |
| /setrepetitiontime HH:MM | Задати час повторень |
| /setnewwordstime HH:MM | Задати час нових слів |
| /setwordscount XY | Задати кількість нових слів і повторень |
| /help | Показати довідку бота |

Повний опис є в [довіднику команд](docs/COMMANDS.md).

## Налаштування

У Docker Compose задаються токен бота, параметри навчання, мовна пара та шлях до CSV-словника. Для робочого розгортання можна вказати публічну HTTPS-адресу webhook.

| Параметр | Типове значення |
|---|---|
| Learning__NewWordsCount | 3 нові слова |
| Learning__RepetitionWordsCount | 10 слів на повторення |
| Learning__AnswerDelaySeconds | 30 секунд |
| Learning__LearnedWordsChunkSize | 10 слів у повідомленні команди /getalllearned |
| Language__TargetLanguage | Polish |
| Language__SourceLanguage | Ukrainian |
| Language__TargetLanguageCode | pl |
| Language__SourceLanguageCode | uk |

Докладніше про webhook: [налаштування webhook](docs/WEBHOOK-SETUP.md).

У прикладі `docker-compose.yml` затримка перед показом відповідей змінена на 5 секунд для швидшої демонстрації; у `appsettings.json` та `appsettings.json.example` типове значення — 30 секунд.

## Розробка

~~~sh
dotnet restore
dotnet build
dotnet run --project ConstantLearning
~~~

Для міграцій бази даних:

~~~sh
dotnet ef migrations add НазваМіграції
dotnet ef database update
~~~

## Структура проєкту

~~~text
ConstantLearning/
├── Configuration/  Налаштування застосунку
├── Controllers/    Telegram webhook
├── Data/           Контекст і сутності бази даних
├── Enums/          Переліки
├── Jobs/           Заплановані завдання Quartz
├── Migrations/     Міграції EF Core
├── Resources/      Повідомлення й списки слів
└── Services/       Логіка бота й навчання
~~~

## Напрям розвитку

У [переліку ідей](docs/IDEAS.md) задокументовано перехід від групових повідомлень до персонального тренера в Telegram Mini App. Це план продукту, а не опис уже реалізованих функцій.

## Готовність словникового контенту

Підготовлено [2 000 схвалених унікальних лем](docs/dictionary/README.md) за погодженим методом Wikisłownik + LLM-редактура. [Звіт готовності](docs/dictionary/readiness.json) і [журнал доказів](docs/dictionary/evidence.jsonl) підтверджують склад випуску. Імпорт у персональну модель виконує [T20](docs/features/personal-trainer-mvp/tasks/T20-dictionary-import.md); чинний груповий бот поки використовує старий CSV. Статус реалізації — у [трекері задач](docs/features/personal-trainer-mvp/tasks/tracker.md).
