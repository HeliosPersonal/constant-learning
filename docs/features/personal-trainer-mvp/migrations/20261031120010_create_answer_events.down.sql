-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_answer_events_version_id;
DROP INDEX IF EXISTS ix_answer_events_entry_id;
DROP INDEX IF EXISTS ix_answer_events_learner_day_outcome;
DROP INDEX IF EXISTS ix_answer_events_learner_entry;
DROP INDEX IF EXISTS ux_answer_events_session_item;
DROP TABLE IF EXISTS answer_events;
