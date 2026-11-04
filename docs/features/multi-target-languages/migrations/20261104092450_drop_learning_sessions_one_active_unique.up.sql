-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
-- CONCURRENTLY: один statement, без транзакції. Промоушен: migrationBuilder.Sql(..., suppressTransaction: true).
-- Критик #1 / AC-04: знімаємо глобальний частковий UNIQUE (одна активна session на learner),
-- бо його заступив мово-свідомий ux_learning_sessions_one_active_lang (крок 20261104092400).
-- Передумова виконання: мово-свідомий UNIQUE уже створено (попередній крок).

DROP INDEX CONCURRENTLY IF EXISTS ux_learning_sessions_one_active;
