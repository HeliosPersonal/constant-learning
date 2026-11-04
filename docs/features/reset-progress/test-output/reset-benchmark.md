# Вимірювання скидання прогресу

Навантажувальний прогін для [T09](../tasks/T09-reset-load-check.md), [PRD §6](../PRD.md#6-non-functional-requirements) і серверна телеметрія з [T08](../tasks/T08-reset-telemetry-counter.md).

## Сценарій

- Генератор навантаження: k6 `grafana/k6:0.57.0`, сценарій [`reset.js`](../../../../tests/load/reset.js), стенд описано в [`tests/load/README.md`](../../../../tests/load/README.md).
- Профіль: 1 VU, 50 послідовних скидань. Кожне скидання виконується для окремого learner із seed-когорти `930000001..930000050`, тобто на повному наборі 2 000 слів і 20 000 відповідей.
- Стенд: PostgreSQL 16, застосунок у Docker з обмеженням 2 CPU і 2 GB RAM, свіжий seed. Прогін виконано окремо від профілю T49.

## Цільова межа

p95 часу скидання ≤ 2 000 ms.

## Результат

| Джерело | Значення | Межа | Вердикт |
| --- | ---: | ---: | --- |
| k6 `reset_duration` p(95) (`reset-summary.json`) | 1 866.7 ms | 2 000 ms | пройдено |
| k6 середнє / медіана / максимум | 1 540 ms / 1 500 ms / 2 050 ms | — | — |
| k6 перевірки (`статус 200`, `erased true`) | 100 з 100 | 100 % | пройдено |
| k6 HTTP-помилки | 0 з 50 | 0 | пройдено |
| Серверна гістограма `trainer_progress_reset_duration_ms` | 50 спостережень, сума 76 893 ms; у bucket ≤ 1 500 ms — 25, у ≤ 2 500 ms — 50 | p95 у bucket ≤ 2 500 ms | не підтверджує ≤ 2 000 ms |
| `trainer_progress_reset_total{outcome="erased"}` | 50 | 50 | пройдено |

k6 застосовує поріг `reset_duration: p(95)<2000` як gate, і прогін завершився з кодом 0. Значення 1 866.7 ms записано як p95 звіту.

Серверна гістограма має bucket-и 1 500 ms і 2 500 ms, тому за процедурою з [`tests/load/README.md`](../../../../tests/load/README.md) p95 визначається як верхня межа bucket `≤ 2 500 ms`. Це не доводить ≤ 2 000 ms на сервері. Клієнтське значення включає мережевий шлях Docker-мережі, тому воно вище за чисту серверну тривалість. Якщо потрібне серверне підтвердження межі, додайте bucket `2000` до гістограми T08. Це окрема задача, і тут її не виконано.

## Артефакти прогону

Сирі файли лежать у `tests/load/results/run/` (каталог не комітиться):

- `reset-summary.json` — клієнтська статистика k6;
- `reset-metrics-before.prom` і `reset-metrics-after.prom` — знімки серверних метрик до й після прогону.

## Відтворення

```powershell
Copy-Item tests/load/results/run/metrics-latest.prom tests/load/results/run/reset-metrics-before.prom
docker run --rm --name constant-learning-load-reset --network constant-learning-load-local `
  -e BASE_URL=http://constant-learning-load-app:8888 -v "${PWD}/tests/load:/scripts" `
  -v "${PWD}/tests/load/results/run:/results" grafana/k6:0.57.0 run `
  --summary-export=/results/reset-summary.json /scripts/reset.js
Copy-Item tests/load/results/run/metrics-latest.prom tests/load/results/run/reset-metrics-after.prom
```
