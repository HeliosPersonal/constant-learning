-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS reminder_schedules (
    learner_id    UUID        NOT NULL PRIMARY KEY REFERENCES learners (id) ON DELETE CASCADE,
    local_time    TIME        NOT NULL,
    days_of_week  SMALLINT    NOT NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);
