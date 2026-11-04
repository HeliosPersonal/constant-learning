-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
-- CONCURRENTLY: один statement, без транзакції. Промоушен: migrationBuilder.Sql(..., suppressTransaction: true).
-- УВАГА: відновлення глобального UNIQUE спрацює лише якщо жоден learner не має >1 активної session
--        (інакше CREATE UNIQUE впаде). Це навмисно: мово-свідома модель допускає кілька активних
--        session на learner, тож повний відкат можливий тільки до появи другої активної мови.

CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_learning_sessions_one_active ON learning_sessions (learner_id) WHERE status = 'active';
