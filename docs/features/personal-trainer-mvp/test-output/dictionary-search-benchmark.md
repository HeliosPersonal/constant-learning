# Вимірювання затримки пошуку у словнику

Фактичний прогін для [T22](../tasks/T22-dictionary-search-api.md), [AC-30](../PRD.md#ac-30---happy-path) та [ADR-0012](../adr/0012-search-via-a-precomputed-table-of-folded-word-forms.md).

- Час UTC: 2026-11-01T11:01:43.9856004+00:00.
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
| 1 | 100 | 11.607 | 30.902 | 37.697 | 31.062 |
| 2 | 100 | 19.894 | 49.953 | 57.377 | 50.129 |
| 3 | 100 | 19.548 | 54.121 | 58.581 | 54.377 |
| 4 | 100 | 21.531 | 54.789 | 62.560 | 54.971 |
| 5 | 100 | 22.468 | 56.384 | 68.931 | 56.549 |
| Усі | 500 | 20.566 | 52.688 | 68.931 | 52.892 |

Умова p95 сервера ≤ 300 ms: виконана. [Сирі вимірювання](dictionary-search-samples.csv).

Команда відтворення з кореня репозиторію (PowerShell):

```powershell
$env:DOCKER_HOST = 'npipe://./pipe/dockerDesktopLinuxEngine'
$env:CI = 'true'
dotnet test ConstantLearning.Tests/ConstantLearning.Tests.csproj --configuration Release --filter FullyQualifiedName~DictionarySearchBenchmarkTests
```
