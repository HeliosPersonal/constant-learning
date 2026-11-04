-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
-- CONCURRENTLY: один statement, без транзакції. Промоушен: migrationBuilder.Sql(..., suppressTransaction: true).

DROP INDEX IF EXISTS ix_learner_words_learner_lang_due;
