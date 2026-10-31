-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_publication_log_actor_created;
DROP INDEX IF EXISTS ix_content_reports_summary_dispatch_id;
DROP INDEX IF EXISTS ix_content_reports_learner_unsummarized;
CREATE INDEX IF NOT EXISTS ix_content_reports_learner_closed ON content_reports (learner_id, closed_at);
DROP INDEX IF EXISTS ux_content_reports_answer_event_id;
CREATE INDEX IF NOT EXISTS ix_content_reports_answer_event_id ON content_reports (answer_event_id);
DROP INDEX IF EXISTS ux_content_reports_open_dedup;
ALTER TABLE content_reports DROP COLUMN IF EXISTS summary_dispatch_id;
ALTER TABLE content_reports DROP COLUMN IF EXISTS dedup_key;
