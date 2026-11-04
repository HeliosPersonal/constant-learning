# Вимірювання затримки екрана «Сьогодні»

Фактичний прогін для [T19](../tasks/T19-today-api.md), [AC-28](../PRD.md), [AC-29](../PRD.md) та контракту [getToday](../contracts/openapi.yaml).

- Час UTC: 2026-11-04T09:13:46.8965185+00:00.
- ОС процесу: Microsoft Windows 10.0.26200; архітектура: X64.
- CPU: Intel64 Family 6 Model 154 Stepping 3, GenuineIntel; доступні логічні CPU: 20.
- Пам’ять, доступна .NET GC: 32429 MiB; runtime: .NET 10.0.4; конфігурація: Release.
- PostgreSQL у локальному Docker: PostgreSQL 16.15 on x86_64-pc-linux-musl, compiled by gcc (Alpine 15.2.0) 15.2.0, 64-bit.
- HTTP через ASP.NET Core TestServer; справжня перевірка Telegram HMAC, scoped EF Core й PostgreSQL. Зовнішню мережеву затримку не вимірює.
- Фікстура: 1 000 learner words, 1 000 опублікованих approved-записів і перекладів, 100 спроб поточної доби, 3 завершені session. Лічильники кожної відповіді перевірено: 600 due, 400 recalled, 3 completed.
- ANALYZE після заповнення; 25 прогрівальних запитів виключено; 200 послідовних GET, паралельність 1.
- Час сервера: middleware від початку HTTP до повернення pipeline, включно з автентифікацією, блокуванням learner, транзакцією, запитами БД, серіалізацією. Перцентилі: nearest rank.

| Запити | p50 сервера, ms | p95 сервера, ms | Максимум сервера, ms | p95 клієнта, ms |
| ---: | ---: | ---: | ---: | ---: |
| 200 | 27.333 | 35.794 | 45.175 | 35.928 |

Умова p95 сервера ≤ 300 ms: виконана. [Сирі вимірювання](today-samples.csv).

Команда відтворення з кореня репозиторію (PowerShell):

```powershell
$env:DOCKER_HOST = 'npipe://./pipe/dockerDesktopLinuxEngine'
$env:CI = 'true'
dotnet test ConstantLearning.Tests/ConstantLearning.Tests.csproj --configuration Release --filter FullyQualifiedName~ConstantLearning.Tests.Learning.Today.TodayBenchmarkTests
```
