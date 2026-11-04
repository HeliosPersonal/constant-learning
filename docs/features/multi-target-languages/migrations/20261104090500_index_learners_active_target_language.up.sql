-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
-- CONCURRENTLY: один statement, без транзакції. Промоушен: migrationBuilder.Sql(..., suppressTransaction: true).

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_learners_active_target_language ON learners (active_target_language);
