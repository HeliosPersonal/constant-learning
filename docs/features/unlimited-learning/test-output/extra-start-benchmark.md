# Вимірювання затримки старту extra-слова й додаткової практики

Навантажувальний прогін для [T12](../tasks/T12-extra-load-check.md), [PRD §6](../PRD.md#6-non-functional-requirements) (QG-2, QG-3), [SAD §10](../sad.md#10-quality-requirements) та AC-01 / AC-08.

## Сценарій

- Генератор навантаження: k6 ([`tests/load/extra-start.js`](../../../../tests/load/extra-start.js)), стенд описано в [`tests/load/README.md`](../../../../tests/load/README.md).
- Профіль: 50 одночасних learner на дію, 1 запит на секунду, 2 хвилини (два блоки `constant-vus` по 50 VU).
- Seed-когорта `940000001..940000100` (LoadHost `tests/load/Program.cs`): план дня виконано — нові слова відкладено на завтра, повторень на сьогодні немає, але лишаються непоказані слова курсу й придатні до практики слова. `940000001..940000050` — старт extra-слова, `940000051..940000100` — старт додаткової практики.
- Перший дотик кожного learner створює extra-session (201), наступні повертають активну (200); обидва шляхи вимірюють тривалість старту.
- Серверні гістограми: `trainer_extra_word_start_ms` і `trainer_extra_practice_start_ms` (весь `StartAsync`, успішні й помилкові шляхи). Авторитетний p95 визначають першим bucket із принаймні 95% спостережень у `extra-metrics-after.prom`. Клієнтський p95 — зі `extra-summary.json`.

## Цільові межі

- p95 старту extra-слова ≤ 500 ms.
- p95 старту додаткової практики (до 20 слів) ≤ 800 ms.

## Результат

Навантажувальний генератор (k6 / стала хвиля навантаження з Docker) **не можна запустити в цьому середовищі**. Числа фіксують у CI або на локальному навантажувальному стенді за процедурою з [`tests/load/README.md`](../../../../tests/load/README.md); таблицю нижче заповнюють збереженими артефактами прогону (`extra-summary.json`, `extra-metrics-after.prom`). Нижче — заготовка; виміряні значення не вигадано.

| Дія | p95 клієнта, ms | p95 сервера, ms | Межа, ms | Вердикт |
| --- | ---: | ---: | ---: | --- |
| Старт extra-слова | _буде заміряно_ | _буде заміряно_ | 500 | _буде заміряно_ |
| Старт додаткової практики | _буде заміряно_ | _буде заміряно_ | 800 | _буде заміряно_ |

Запуск (з [`tests/load/README.md`](../../../../tests/load/README.md), окремо від профілю T49 і сценарію скидання, на свіжому seed):

```powershell
Copy-Item tests/load/results/run/metrics-latest.prom tests/load/results/run/extra-metrics-before.prom
docker run --rm --name constant-learning-load-extra --network constant-learning-load-local `
  -e BASE_URL=http://constant-learning-load-app:8888 -v "${PWD}/tests/load:/scripts" `
  -v "${PWD}/tests/load/results/run:/results" grafana/k6:0.57.0 run `
  --summary-export=/results/extra-summary.json /scripts/extra-start.js
Copy-Item tests/load/results/run/metrics-latest.prom tests/load/results/run/extra-metrics-after.prom
```
