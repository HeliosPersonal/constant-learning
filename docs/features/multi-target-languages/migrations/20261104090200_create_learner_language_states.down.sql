-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

DROP INDEX IF EXISTS ix_learner_language_states_target_language;
DROP TABLE IF EXISTS learner_language_states;
