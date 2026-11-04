-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
-- CONCURRENTLY: один statement, без транзакції. Промоушен: migrationBuilder.Sql(..., suppressTransaction: true).

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_learning_sessions_learner_lang_active ON learning_sessions (learner_id, target_language) WHERE status = 'active';
