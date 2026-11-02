# Локальне навантажувальне тестування

Сценарій відповідає [T49](../../docs/features/personal-trainer-mvp/tasks/T49-load-test.md). Це локальний стенд: CI лише завантажує збережені результати, а запуск навантаження і браузера в CI не налаштований.

```mermaid
flowchart LR
  Seed["Нова тестова БД і seed"] --> Init["ANALYZE і прогрівання"]
  Init --> Load["100 VU / 15 хв"]
  Init --> Browser["50 відкриттів браузера"]
  Load --> Check["Метрики та звірка"]
  Browser --> Check
  Init --> Reminders["1000 нагадувань / реальний час"]
  Reminders --> Check
  Check --> Report["Локальний звіт і CI-артефакт"]
```

Окремий контейнер застосунку має обмеження `--cpus 2 --memory 2g`; PostgreSQL працює окремо. Seed створює 2 000 опублікованих approved записів, 100 learner із 1 000 словами кожен (3 показані сьогодні і 997 належать до повторення) та окрему когорту 1 000 отримувачів нагадувань. Слова імітаційні, леми складаються з літер. Після seed виконується `ANALYZE` таблиць словника, learner, сесій, відповідей і нагадувань до старту вимірювання. Час — `SystemClock`; Telegram замінено заглушкою, що записує час виклику. Production Quartz планує та доставляє нагадування; сторонні jobs і webhook вимкнено.

Профіль до вимірювання: 100 постійних VU протягом 15 хвилин, кожна ітерація відкриває Today, виконує 10 пошуків із префіксами довжиною 1–5 (150 мс між символами, 1 с між пошуками) і проходить заняття із 20 повторень (5 с на відповідь). Три чверті відповідей правильні, чверть містить одну помилку; кожну оцінку підтверджує `continue`. 50 окремих браузерних контекстів відкривають справжній Mini App кожні 15 с одночасно з навантаженням; мережеву відповідь імітовано лише для Telegram SDK. Час `today_ready_ms` береться з фактичного запиту telemetry.

Серверні гістограми `trainer_answer_duration_ms` і `trainer_dictionary_search_duration_ms` зберігають накопичені bucket-лічильники, а не окремі тривалості. Тому межу p95 визначають першим bucket із принаймні 95% спостережень; значення HTTP-клієнта не є серверним p95. Перевірка подій зіставляє `answer_events` із різними оціненими `session_items`, включно з відсутніми та повторними зв’язками. Raw-файли і фактичний результат публікуються разом зі звітом лише після завершеного прогону.

Для відтворення з кореня репозиторію зберіть `docker build -f tests/load/Dockerfile -t constant-learning-load-local .`, створіть окрему Docker-мережу та PostgreSQL 16 із новою порожньою базою. Передайте застосунку `ConnectionStrings__DefaultConnection`, підключіть каталог результатів у `/results`, опублікуйте порт 8888 лише на localhost і встановіть обмеження ресурсів, наведені вище. Дочекайтеся `LOAD_READY` та перевірте кількість записів у базі. Починайте вимірювання до планової хвилини нагадувань із достатнім запасом для 15 хвилин навантаження.

Запустіть `grafana/k6:0.57.0 run --out json=/results/k6-raw.json --summary-export=/results/k6-summary.json /scripts/learners.js` з `BASE_URL` контейнера застосунку; окремо, під навантаженням, виконайте `node tests/load/browser.mjs`, задавши `RESULTS_DIR` до каталогу результатів. До і після навантаження збережіть `metrics-latest.prom`, а після запланованої хвилини й двох хвилин очікування — `reminder-sends.csv`, журнал помилок та результат [SQL-звірки](reconcile.sql). Для аналізу локального каталогу `results/final` призначено `python tests/load/summarize.py`. Вимірюйте в ізоляції від збірок та інших тестів; не оновлюйте seed під час профілю.

Нижче — PowerShell-приклад із кореня репозиторію. Результати першого й наступних прогонів мають окремі каталоги. Пароль і токен у стенді призначені лише для локальних одноразових контейнерів.

```powershell
New-Item -ItemType Directory -Force tests/load/results/run | Out-Null
docker network create constant-learning-load-local
docker build -f tests/load/Dockerfile -t constant-learning-load-local .
docker run -d --name constant-learning-load-db --network constant-learning-load-local `
  -e POSTGRES_DB=load -e POSTGRES_USER=load -e POSTGRES_PASSWORD=load-local-only postgres:16-alpine
docker run -d --name constant-learning-load-app --network constant-learning-load-local `
  --cpus 2 --memory 2g -p 127.0.0.1:18889:8888 `
  -e 'ConnectionStrings__DefaultConnection=Host=constant-learning-load-db;Database=load;Username=load;Password=load-local-only' `
  -v "${PWD}/tests/load/results/run:/results" constant-learning-load-local
# Дочекайтеся LOAD_READY після seed, ANALYZE та прогрівання.
docker logs --tail 20 constant-learning-load-app
Get-Content tests/load/results/run/reminder-due.txt
docker inspect constant-learning-load-app --format '{{.Image}} {{.HostConfig.NanoCpus}} {{.HostConfig.Memory}}' |
  Set-Content tests/load/results/run/container-config.txt
Copy-Item tests/load/results/run/metrics-latest.prom tests/load/results/run/metrics-before.prom
```

Одночасно запустіть дві наступні команди в окремих PowerShell-терміналах; другу — після початку першої. Зберігайте повний час 15 хвилин і graceful stop:

```powershell
docker run --rm --name constant-learning-load-k6 --network constant-learning-load-local `
  -e BASE_URL=http://constant-learning-load-app:8888 -v "${PWD}/tests/load:/scripts" `
  -v "${PWD}/tests/load/results/run:/results" grafana/k6:0.57.0 run `
  --log-output=file=/results/k6-errors.log --out json=/results/k6-raw.json `
  --summary-export=/results/k6-summary.json /scripts/learners.js
```

```powershell
$env:RESULTS_DIR='tests/load/results/run'
node tests/load/browser.mjs
```

Після завершення k6 скопіюйте актуальні server metrics. Після запланованої хвилини і повних 120 секунд виконайте звірку, збережіть JSON-результат для аналізатора, потім зупиніть лише створені для цього прогону ресурси. Не запускайте це до завершення хвилі нагадувань:

```powershell
Copy-Item tests/load/results/run/metrics-latest.prom tests/load/results/run/metrics-after.prom
Get-Content -Raw tests/load/reconcile.sql | docker exec -i constant-learning-load-db psql -U load -d load |
  Set-Content tests/load/results/run/reconcile.txt
# reconciliation.json формують запитом, наведеним у reconcile-json.sql.
Get-Content -Raw tests/load/reconcile-json.sql | docker exec -i constant-learning-load-db psql -U load -d load -At |
  Set-Content -Encoding utf8 tests/load/results/run/reconciliation.json
$env:LOAD_RESULTS_DIR='tests/load/results/run'
python tests/load/summarize.py
docker rm -f --volumes constant-learning-load-app constant-learning-load-db
docker network rm constant-learning-load-local
```
