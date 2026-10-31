-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_learning_sessions_learner_day;
DROP INDEX IF EXISTS ix_learning_sessions_learner_completed;
DROP INDEX IF EXISTS ux_learning_sessions_one_active;
DROP TABLE IF EXISTS learning_sessions;
