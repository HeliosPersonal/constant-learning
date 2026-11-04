-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
-- CONCURRENTLY: один statement, без транзакції. Промоушен: migrationBuilder.Sql(..., suppressTransaction: true).

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_answer_events_learner_lang_day_outcome ON answer_events (learner_id, target_language, learner_day, outcome);
