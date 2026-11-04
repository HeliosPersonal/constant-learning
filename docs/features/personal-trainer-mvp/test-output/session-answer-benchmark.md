# Вимірювання затримки відповіді на картку

Фактичний прогін для [T17](../tasks/T17-answer-item.md), [PRD §6](../PRD.md#6-non-functional-requirements) та [ADR-0008](../adr/0008-keep-the-learning-session-server-side-with-per-card-idempotency.md).

- Час UTC: 2026-11-04T09:13:02.6120231+00:00.
- ОС: Microsoft Windows 10.0.26200; архітектура: X64; runtime: .NET 10.0.4; Release.
- Логічні CPU: 20; доступна .NET GC пам’ять: 32429 MiB.
- PostgreSQL у локальному Docker: PostgreSQL 16.15 on x86_64-pc-linux-musl, compiled by gcc (Alpine 15.2.0) 15.2.0, 64-bit.
- Фікстура: 1 000 approved-записів, 1 000 learner words до повторення; ANALYZE після заповнення. Справжні Telegram HMAC, EF Core, транзакції та PostgreSQL 16 через ASP.NET Core TestServer.
- 20 прогрівальних відповідей виключено; виміряно 200 послідовних відповідей у 10 сесіях по 20 карток. Запити старту session та переходу continue не входять у вибірку; паралельність 1.
- Серверний middleware вимірює весь HTTP pipeline: автентифікацію, БД, оцінку, збереження результату й серіалізацію; зовнішня мережа не моделюється. Перцентиль: nearest rank.

| Запити | p50 сервера, ms | p95 сервера, ms | Максимум сервера, ms | p95 клієнта, ms |
| ---: | ---: | ---: | ---: | ---: |
| 200 | 29.961 | 39.507 | 52.683 | 39.712 |

Умова p95 сервера ≤ 500 ms: виконана. [Сирі вимірювання](session-answer-samples.csv).

```powershell
$env:DOCKER_HOST = 'npipe://./pipe/dockerDesktopLinuxEngine'
$env:CI = 'true'
dotnet test ConstantLearning.Tests/ConstantLearning.Tests.csproj --configuration Release --filter FullyQualifiedName~AnswerSessionBenchmarkTests
```
