-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS answer_neutralizations (
    id                 UUID        NOT NULL PRIMARY KEY,
    answer_event_id    UUID        NOT NULL REFERENCES answer_events (id) ON DELETE CASCADE,
    content_report_id  UUID        NOT NULL REFERENCES content_reports (id),
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_answer_neutralizations_answer_event ON answer_neutralizations (answer_event_id);
CREATE INDEX IF NOT EXISTS ix_answer_neutralizations_content_report_id ON answer_neutralizations (content_report_id);
