-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_content_reports_resulting_version_id;
DROP INDEX IF EXISTS ix_content_reports_answer_event_id;
DROP INDEX IF EXISTS ix_content_reports_version_id;
DROP INDEX IF EXISTS ix_content_reports_entry_id;
DROP INDEX IF EXISTS ix_content_reports_status_created;
DROP INDEX IF EXISTS ix_content_reports_learner_closed;
DROP INDEX IF EXISTS ix_content_reports_learner_created;
DROP TABLE IF EXISTS content_reports;
