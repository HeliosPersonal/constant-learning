# Інструкція експлуатації пілоту

Власник і виконавець операцій — Viacheslav Melnichenko. Документ реалізує операційну частину [T45](tasks/T45-deployment-fixes.md) для вимог [PRD §6](PRD.md#6-non-functional-requirements), [PRD §6.1](PRD.md#61-security--privacy), [SAD §7](sad.md#7-deployment-view), [SAD §11](sad.md#11-risks-and-technical-debt) та [ADR-0004](adr/0004-retire-the-group-model-without-data-migration.md). Вихідні обмеження описує [карта архітектури](../../architecture-map.md).

## Поточна конфігурація та межі перевірки

Docker збирається з кореня репозиторію: Node 22 пакує Mini App у `wwwroot`, .NET 10 публікує API зі статичними файлами. Runtime `aspnet:10.0-noble` містить ICU й системну tzdata; застосунок працює без прав root на порту `8888`. У Kubernetes — `replicas: 1` і `Recreate`, тому під час оновлення є коротке вікно недоступності. Друга репліка не підтримується цим пілотом.

CI для pull request збирає бекенд і виконує unit, integration та contract тести; Docker обов'язковий, відсутність Docker у CI спричиняє падіння PostgreSQL-тестів. Push у `main` повторює ці перевірки та публікує образ із тегом commit SHA. Після одноразового налаштування й увімкнення автоматизації CI розгортає production за digest; порядок, поточні обмеження доступу runner і rollback описує [інструкція деплою](../../deployment.md). Перевірки типів, lint, unit і E2E фронтенду виконуються локально; Docker лише генерує токени теми й запускає Vite build.

Наведені нижче резервування, зовнішній моніторинг, контакт для видалення й пробне відновлення — операції, які власник повинен налаштувати й зафіксувати **до пілоту**. Наявність цього документа не означає, що вони вже активні. Локальна перевірка не підтверджує доступність зовнішнього HTTPS, реєстрацію webhook чи production-БД.

## Локальна перевірка образу

Виконати з кореня репозиторію за наявності Docker Desktop у режимі Linux containers і PowerShell 7:

```powershell
docker build -f ConstantLearning/Dockerfile -t constant-learning:pilot-local .
pwsh -NoProfile -File tools/deployment/smoke.ps1
kubectl kustomize k8s/overlays/staging
```

[Скрипт smoke](../../../tools/deployment/smoke.ps1) збирає окремий target `container-smoke` з тим самим runtime і статичними файлами; запускає справжній `Program`, застосовує міграції до тимчасового PostgreSQL 16, але вилучає webhook і Quartz через тестовий host. Production target `final` не містить helper або його залежностей. Ізольована Docker-мережа `--internal` не має зовнішнього маршруту; використовуються лише фіктивний Telegram token та тимчасова БД.

Успішний результат: `TIMEZONE_OK` для `Europe/Warsaw` і `Asia/Tokyo` із зимовими та літніми UTC offset, `ICU_OK=pl-PL`, `CONTAINER_SMOKE_READY=8888` і `PROBE_OK`. Додатково перевіряються оболонка Mini App, JS-ресурс, вкладений SPA-маршрут та `404` для невідомого API. Probe виконується з другого контейнера через внутрішню мережу до порту `8888`; порти на host не публікуються. Скрипт прибирає свої контейнери, том PostgreSQL і мережу в `finally`; локальні образи та build cache залишаються для повторної збірки.

## Підготовка релізу

1. Зберегти SHA попереднього й нового образів та застосовані маніфести в журналі релізу. Використовувати незмінний SHA, а не `latest`.
2. У захищеному сховищі секретів налаштувати `ConnectionStrings__DefaultConnection`, `Telegram__BotToken`, `Telegram__WebhookUrl`, `Telegram__SecretToken` і `Telegram__MiniAppUrl`. За [T32](tasks/T32-bot-webhook.md), `Telegram__SecretToken` — окремий випадковий секрет із 1–256 символів `A-Z`, `a-z`, `0-9`, `_`, `-`; сервер передає його в `SetWebhook` і перевіряє заголовок `X-Telegram-Bot-Api-Secret-Token`. `Telegram__WebhookUrl` — HTTPS-адреса сервера або повний шлях `/api/telegram/webhook` без query чи fragment; `Telegram__MiniAppUrl` — повна HTTPS-адреса Mini App для кнопки `/start`. Прикладні значення `REPLACED_BY_CI` в [Telegram Secret](../../../k8s/base/secret-telegram.yaml) — заповнювачі; ці шаблони виключено з Kustomize. Production-секрети формує [сценарій автоматичного деплою](../../../tools/deployment/deploy.py) за [інструкцією налаштування](../../deployment.md). Не комітити справжні секрети й не копіювати їх у журнал операцій.
3. Перевірити render цільового overlay (для автоматичного деплою — production): probes `GET /api/telegram/health`, порт `8888`, ключ `ConnectionStrings__DefaultConnection` у Secret і Deployment; одна репліка та `Recreate`. Налаштувати зовнішній HTTPS для API, Mini App і webhook на одному домені.
4. До першого пілоту виконати локальні перевірки Mini App за [T34](tasks/T34-miniapp-skeleton.md), один Telegram smoke за [T46](tasks/T46-e2e-critical-flows.md) та навантажувальну перевірку за [T49](tasks/T49-load-test.md). Зафіксувати результат, не приписувати контейнерному smoke ці перевірки.
5. Перед [T44](tasks/T44-migrate-drop-group-model.md) обов'язково виконати пробне відновлення, описане нижче. Наявний код групової моделі й автоматичні міграції ще можуть бути присутні; цей документ не засвідчує завершення [T43](tasks/T43-retire-group-code.md) або [T44](tasks/T44-migrate-drop-group-model.md).

## Резервні копії

Власник налаштовує незалежний від кластера backup host або керований сервіс: **щодня о 01:00 UTC**, зашифрована повна логічна копія PostgreSQL, зберігання **14 днів** у сховищі поза кластером. Ключ розшифрування зберігається окремо від копій і кластера; доступ має власник. Політика сховища видаляє копії старше 14 днів і незавершені завантаження. Перед руйнівною міграцією створюється додаткова копія з тією самою політикою зберігання.

Приклад кроку зовнішнього backup job для Bash, `pg_dump` 16, `age` і AWS CLI; `PGSERVICE`/`PGPASSFILE` налаштовані на backup host, змінні сховища й публічний recipient передаються з його захищеної конфігурації:

```bash
set -euo pipefail
umask 077
: "${PGSERVICE:?}" "${PGPASSFILE:?}" "${BACKUP_BUCKET:?}" "${AGE_RECIPIENT:?}"
backup_stamp=$(date -u +%Y%m%dT%H%M%SZ)
backup_dir=$(mktemp -d)
trap 'rm -rf -- "$backup_dir"' EXIT
pg_dump --format=custom --no-owner --no-acl --file="$backup_dir/pilot.dump"
age --recipient "$AGE_RECIPIENT" --output "$backup_dir/pilot.dump.age" "$backup_dir/pilot.dump"
(cd "$backup_dir" && sha256sum pilot.dump.age > pilot.dump.age.sha256)
aws s3 cp "$backup_dir/pilot.dump.age" "s3://$BACKUP_BUCKET/pilot/$backup_stamp/pilot.dump.age"
aws s3 cp "$backup_dir/pilot.dump.age.sha256" "s3://$BACKUP_BUCKET/pilot/$backup_stamp/pilot.dump.age.sha256"
```

Після кожного запуску зберегти UTC-час, код завершення, розмір копії й ключ об'єкта; власник щодня перевіряє, що останній успішний backup молодший за 24 години. Невдалий backup повторити й розібрати до наступного релізу. Це журнал операцій резервування; єдиний активний сигнал моніторингу доступності пілоту описаний нижче.

## Пробне відновлення та відновлення після збою

Перед [T44](tasks/T44-migrate-drop-group-model.md) зупинити зміни даних на час фінальної копії, зробити backup, завантажити його **зі сховища поза кластером** і відновити в окрему порожню БД. Не підключати до неї production-застосунок або Telegram. Приклад після завантаження `.age` і `.sha256` у захищений робочий каталог:

```bash
set -euo pipefail
umask 077
: "${RESTORE_DATABASE_URL:?}" "${AGE_IDENTITY_FILE:?}"
sha256sum --check pilot.dump.age.sha256
age --decrypt --identity "$AGE_IDENTITY_FILE" --output pilot.dump pilot.dump.age
pg_restore --list pilot.dump > restore-manifest.txt
pg_restore --dbname="$RESTORE_DATABASE_URL" --exit-on-error --no-owner --no-acl --single-transaction pilot.dump
psql "$RESTORE_DATABASE_URL" -v ON_ERROR_STOP=1 -c 'SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
```

Оператор окремо перевіряє, що `RESTORE_DATABASE_URL` вказує на **окрему тестову БД**, а не джерело копії. Порівняти схему, історію міграцій і кількості рядків усіх таблиць зі збереженим під час backup звітом, зокрема `Words`, `LearnedWords` і `ChatRegistrations` перед їх видаленням. Зафіксувати SHA копії, час restore, результати перевірок і версію сумісного образу; лише успішна перевірка дозволяє виконати [T44](tasks/T44-migrate-drop-group-model.md). Видалити тестову БД та розшифровані файли після перевірки.

Після руйнівної міграції повернення попереднього коду **не відновлює схему та дані** ([ADR-0004](adr/0004-retire-the-group-model-without-data-migration.md)). Порядок recovery: зупинити один застосунок і записи; зберегти копію поточного стану для розбору; відновити перевірену копію в нову окрему БД; повторно виконати вже завершені видалення learner; перевірити схему й дані; вибрати сумісний образ; переключити рядок підключення й запустити один екземпляр. Перевірити health, Mini App і працездатність сценарію навчання. Втрата змін після точки backup та фактичний час recovery фіксуються в журналі інциденту; автоматичного rollback БД немає.

## Ручне видалення learner

До онбордингу власник публікує свій контакт у повідомленні про дані за [PRD §6.1](PRD.md#61-security--privacy). Запит приймається через цей контакт; строк виконання — **7 календарних днів від отримання**.

1. Зареєструвати час отримання, граничну дату, статус і мінімальний ідентифікатор запиту в захищеному журналі. Підтвердити власність облікового запису через той самий особистий Telegram-акаунт, без запиту token, `initData` чи пароля.
2. Визначити `learners.id` за перевіреним Telegram user id; не вносити Telegram id у загальнодоступні логи. На час транзакції зупинити один застосунок, щоб фоновий процес не надсилав повідомлення та повторна автентифікація не створила learner під час видалення.
3. Перед видаленням зняти актуальний перелік FK, включно з непрямими залежностями. Поточна схема має `reminder_schedules.learner_id ON DELETE CASCADE`; це **не** гарантія каскаду для всіх наступних модулів. Приклад інвентаризації всіх FK у фактичній БД:

```sql
SELECT conrelid::regclass AS child_table,
       confrelid::regclass AS parent_table,
       conname, pg_get_constraintdef(oid) AS definition
FROM pg_constraint
WHERE contype = 'f'
ORDER BY confrelid::regclass::text, conrelid::regclass::text, conname;
```

4. У транзакції видалити персональні дані відповідно до реально застосованих міграцій. Для поточної схеми `DELETE FROM learners WHERE id = :learner_id` каскадно видаляє `reminder_schedules`, `learner_words`, `learning_sessions` → `session_items` та `answer_events`; перед виконанням параметр прив'язати через SQL-клієнт, перевірити рівно один learner. FK `answer_events.session_item_id` має `SET NULL`, але всі власні `answer_events` одночасно видаляються каскадом за learner. Коли будуть реалізовані решта таблиць із [моделі даних](data-model.md), додатково перевірити `answer_neutralizations`, `content_reports`, їхні verification jobs і `notification_dispatches`. Зв'язки з історією словника можуть блокувати каскад: `publication_log.actor_learner_id` слід знеособити до `NULL`, а зв'язки reports з версіями й журналом відповідей — розірвати або видалити в правильному порядку згідно з фактичними FK. Не видаляти спільний опублікований словник. Авторський вільний текст, псевдонім і payload з персональними даними також видалити або знеособити; один успішний `DELETE` без цієї перевірки не означає завершення.
5. До `COMMIT` перевірити відсутність learner і всіх його персональних рядків/посилань; у разі помилки зробити `ROLLBACK`, уточнити порядок залежностей і повторити в межах строку. Запустити застосунок, перевірити health. Не автентифікуватися від імені видаленого learner для перевірки, бо це може створити новий профіль.
6. Повідомити заявника про завершення й залишити у захищеному журналі дату, результат і мінімальну прив'язку, потрібну для повторного видалення після restore. Зашифровані копії можуть містити попередні дані до завершення **14 днів** зберігання; не обіцяти негайного видалення з незмінних копій. Після restore до відкриття доступу повторно застосувати завершені запити видалення. Якщо користувач згодом сам знову відкриє trainer, може створитися новий профіль.

## Зовнішня перевірка доступності

До пілоту власник налаштовує сервіс **поза кластером**, що раз на **1 хвилину** виконує HTTPS `GET https://<pilot-domain>/api/telegram/health`, очікує `200` та JSON `status=healthy`, із timeout 10 секунд. Після **трьох послідовних невдалих перевірок** сервіс надсилає сповіщення на контакт власника; успішна перевірка обнуляє лічильник і закриває інцидент. Зафіксувати постачальника, фактичний URL, контакт і результат тестового сповіщення в приватному журналі запуску.

Ціль доступності — **99,5% за календарний місяць** за [PRD §6](PRD.md#6-non-functional-requirements). Зберігати результати всіх хвилинних перевірок; відсутні спостереження позначати як невідомі, а не як успіх. Endpoint показує, що процес обслуговує HTTP; він не перевіряє Telegram, актуальність backup або повний навчальний потік. Після сповіщення перевірити зовнішній HTTPS, стан Pod, його журнал і доступність PostgreSQL; recovery з копії виконувати лише за описаним вище порядком. Після відновлення вручну підтвердити сценарій навчання.
