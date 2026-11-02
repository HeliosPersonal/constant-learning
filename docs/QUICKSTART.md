# Швидкий запуск

## Потрібно підготувати

- Docker і Docker Compose.
- Токен Telegram-бота від [@BotFather](https://t.me/BotFather).
- Доступ до Telegram-групи, де ви можете додати бота.

## 1. Отримайте токен

У Telegram відкрийте @BotFather, надішліть /newbot і виконайте його інструкції. Збережіть виданий токен і не додавайте його до Git.

## 2. Налаштуйте проєкт

Клонуйте репозиторій:

~~~sh
git clone https://github.com/ViacheslavMelnichenko/constant-learning.git
cd constant-learning
~~~

Задайте TELEGRAM_BOT_TOKEN у змінних середовища або у своєму локальному файлі налаштувань Docker Compose.

Для робочого розгортання задайте публічну адресу HTTPS, що закінчується на /api/telegram/webhook. Локальне тестування через ngrok описане в розділі нижче. Якщо адресу webhook не задати, бот надсилатиме заплановані повідомлення, але не отримуватиме команди й інші вхідні оновлення Telegram.

## 3. Запустіть сервіси

~~~sh
docker compose up -d
~~~

Перевірте журнали:

~~~sh
docker compose logs -f app
~~~

## 4. Додайте бота до групи

Додайте бота до Telegram-групи та надішліть:

~~~text
/startlearning
~~~

Типовий розклад: повторення о 09:00 і нові слова о 20:00. Його можна змінити командами /setrepetitiontime та /setnewwordstime.

## Локальна розробка з webhook через ngrok

1. Встановіть ngrok із [офіційного сайту](https://ngrok.com/).
2. Запустіть тунель на порт застосунку:

~~~sh
ngrok http 8888
~~~

3. Скопіюйте HTTPS-адресу, яку показав ngrok, і задайте її як адресу webhook разом із шляхом /api/telegram/webhook.
4. Перезапустіть застосунок:

~~~sh
docker compose restart app
~~~

Безкоштовна адреса ngrok зазвичай змінюється після перезапуску тунелю. Тоді оновіть адресу webhook і перезапустіть бота.

## Типові дії

Змінити розклад:

~~~text
/setrepetitiontime 08:00
/setnewwordstime 21:00
~~~

Змінити кількість нових слів і повторень, наприклад на 2 нових і 5 для повторення:

~~~text
/setwordscount 25
~~~

Зупинити сервіси:

~~~sh
docker compose down
~~~

Дивіться також [довідник команд](COMMANDS.md), [налаштування webhook](WEBHOOK-SETUP.md) і [головний опис проєкту](../README.md).
