# Вимірювання затримки пошуку у словнику

Фактичний прогін для [T22](../tasks/T22-dictionary-search-api.md), [AC-30](../PRD.md) та [ADR-0012](../adr/0012-search-via-a-precomputed-table-of-folded-word-forms.md).

- Час UTC: 2026-11-04T09:12:05.2713536+00:00.
- ОС процесу: Microsoft Windows 10.0.26200; архітектура: X64.
- CPU зі змінної середовища PROCESSOR_IDENTIFIER: Intel64 Family 6 Model 154 Stepping 3, GenuineIntel.
- Доступні логічні CPU: 20; пам’ять, доступна .NET GC: 32429 MiB.
- Runtime: .NET 10.0.4; конфігурація: Release.
- PostgreSQL у локальному Docker: PostgreSQL 16.15 on x86_64-pc-linux-musl, compiled by gcc (Alpine 15.2.0) 15.2.0, 64-bit.
- HTTP через ASP.NET Core TestServer; справжня перевірка Telegram HMAC та запити EF Core до PostgreSQL; мережеву затримку зовнішнього клієнта не вимірює.
- Фікстура: 2 000 опублікованих approved-записів, 26 000 форм, 2 000 перекладів. ANALYZE після заповнення.
- 25 прогрівальних запитів не входять у вибірку. 500 послідовних запитів, паралельність 1, по 100 кожної довжини: `a`, `za`, `zab`, `zaba`, `zabam`.
- Серверний час: middleware від початку обробки HTTP до повернення pipeline, включно з автентифікацією, запитами БД і серіалізацією. Перцентиль: nearest rank.

| Довжина | Запити | p50 сервера, ms | p95 сервера, ms | Максимум сервера, ms | p95 клієнта, ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | 100 | 13.840 | 18.880 | 29.484 | 19.001 |
| 2 | 100 | 22.904 | 26.985 | 35.894 | 27.193 |
| 3 | 100 | 23.188 | 26.200 | 33.400 | 26.345 |
| 4 | 100 | 24.004 | 29.074 | 35.104 | 29.262 |
| 5 | 100 | 26.586 | 33.116 | 61.628 | 33.311 |
| Усі | 500 | 23.560 | 28.699 | 61.628 | 28.815 |

Умова p95 сервера ≤ 300 ms: виконана. [Сирі вимірювання](dictionary-search-samples.csv).

Команда відтворення з кореня репозиторію (PowerShell):

```powershell
$env:DOCKER_HOST = 'npipe://./pipe/dockerDesktopLinuxEngine'
$env:CI = 'true'
dotnet test ConstantLearning.Tests/ConstantLearning.Tests.csproj --configuration Release --filter FullyQualifiedName~DictionarySearchBenchmarkTests
```
