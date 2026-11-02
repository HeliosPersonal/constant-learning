# Вимірювання затримки пошуку у словнику

Фактичний прогін для [T22](../tasks/T22-dictionary-search-api.md), [AC-30](../PRD.md#ac-30---happy-path) та [ADR-0012](../adr/0012-search-via-a-precomputed-table-of-folded-word-forms.md).

- Час UTC: 2026-11-02T07:25:21.8919393+00:00.
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
| 1 | 100 | 24.182 | 41.271 | 52.055 | 41.485 |
| 2 | 100 | 41.896 | 78.788 | 95.746 | 79.165 |
| 3 | 100 | 42.791 | 71.909 | 104.490 | 72.159 |
| 4 | 100 | 43.807 | 78.978 | 102.580 | 79.319 |
| 5 | 100 | 47.837 | 80.021 | 104.250 | 80.198 |
| Усі | 500 | 42.791 | 75.291 | 104.490 | 75.458 |

Умова p95 сервера ≤ 300 ms: виконана. [Сирі вимірювання](dictionary-search-samples.csv).

Команда відтворення з кореня репозиторію (PowerShell):

```powershell
$env:DOCKER_HOST = 'npipe://./pipe/dockerDesktopLinuxEngine'
$env:CI = 'true'
dotnet test ConstantLearning.Tests/ConstantLearning.Tests.csproj --configuration Release --filter FullyQualifiedName~DictionarySearchBenchmarkTests
```
