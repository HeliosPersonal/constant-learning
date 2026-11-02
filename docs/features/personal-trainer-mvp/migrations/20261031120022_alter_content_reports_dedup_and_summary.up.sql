-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
-- Синхронізація з PRD після другого clarify-prd: дедуплікація відкритих reports (AC-51),
-- один report на оцінену відповідь (AC-47), підсумок «відтоді» (AC-37), список застосованих змін (AC-48).
-- Таблиці нові в цій фічі й ще порожні під час застосування, тому індекси без CONCURRENTLY.
ALTER TABLE content_reports ADD COLUMN IF NOT EXISTS dedup_key VARCHAR(64) NULL;
ALTER TABLE content_reports ADD COLUMN IF NOT EXISTS summary_dispatch_id UUID NULL REFERENCES notification_dispatches (id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_content_reports_open_dedup ON content_reports (learner_id, dedup_key) WHERE closed_at IS NULL AND dedup_key IS NOT NULL;
DROP INDEX IF EXISTS ix_content_reports_answer_event_id;
CREATE UNIQUE INDEX IF NOT EXISTS ux_content_reports_answer_event_id ON content_reports (answer_event_id);
DROP INDEX IF EXISTS ix_content_reports_learner_closed;
CREATE INDEX IF NOT EXISTS ix_content_reports_learner_unsummarized ON content_reports (learner_id, closed_at) WHERE closed_at IS NOT NULL AND summary_dispatch_id IS NULL;
CREATE INDEX IF NOT EXISTS ix_content_reports_summary_dispatch_id ON content_reports (summary_dispatch_id);
CREATE INDEX IF NOT EXISTS ix_publication_log_actor_created ON publication_log (actor_kind, created_at);
