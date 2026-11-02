-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS learning_sessions (
    id                UUID        NOT NULL PRIMARY KEY,
    learner_id        UUID        NOT NULL REFERENCES learners (id) ON DELETE CASCADE,
    learner_day       DATE        NOT NULL,
    ends_at           TIMESTAMPTZ NOT NULL,
    status            VARCHAR(16) NOT NULL,
    last_activity_at  TIMESTAMPTZ NULL,
    completed_at      TIMESTAMPTZ NULL,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_learning_sessions_one_active ON learning_sessions (learner_id) WHERE status = 'active';
CREATE INDEX IF NOT EXISTS ix_learning_sessions_learner_completed ON learning_sessions (learner_id, completed_at);
CREATE INDEX IF NOT EXISTS ix_learning_sessions_learner_day ON learning_sessions (learner_id, learner_day);
