-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS answer_events (
    id                 UUID         NOT NULL PRIMARY KEY,
    learner_id         UUID         NOT NULL REFERENCES learners (id) ON DELETE CASCADE,
    entry_id           UUID         NOT NULL REFERENCES dictionary_entries (id),
    version_id         UUID         NOT NULL REFERENCES entry_versions (id),
    session_item_id    UUID         NULL REFERENCES session_items (id) ON DELETE SET NULL,
    exercise_kind      VARCHAR(24)  NOT NULL,
    outcome            VARCHAR(24)  NOT NULL,
    raw_answer         VARCHAR(200) NULL,
    normalized_answer  VARCHAR(200) NULL,
    answered_at        TIMESTAMPTZ  NOT NULL,
    learner_day        DATE         NOT NULL,
    time_zone          VARCHAR(64)  NOT NULL,
    due_on             DATE         NULL,
    interval_before    INTEGER      NULL,
    scheduler_version  VARCHAR(16)  NOT NULL,
    state_before       JSONB        NOT NULL,
    created_at         TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_answer_events_session_item ON answer_events (session_item_id);
CREATE INDEX IF NOT EXISTS ix_answer_events_learner_entry ON answer_events (learner_id, entry_id, answered_at);
CREATE INDEX IF NOT EXISTS ix_answer_events_learner_day_outcome ON answer_events (learner_id, learner_day, outcome);
CREATE INDEX IF NOT EXISTS ix_answer_events_entry_id ON answer_events (entry_id);
CREATE INDEX IF NOT EXISTS ix_answer_events_version_id ON answer_events (version_id);
