-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
-- CONCURRENTLY: один statement, без транзакції. Промоушен: migrationBuilder.Sql(..., suppressTransaction: true).

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_learner_words_target_language ON learner_words (target_language);
