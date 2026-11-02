-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_learner_words_entry_id;
DROP INDEX IF EXISTS ix_learner_words_learner_status;
DROP INDEX IF EXISTS ix_learner_words_learner_due;
DROP INDEX IF EXISTS ux_learner_words_learner_entry;
DROP TABLE IF EXISTS learner_words;
