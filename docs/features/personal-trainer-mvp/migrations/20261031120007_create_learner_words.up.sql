-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS learner_words (
    id                             UUID        NOT NULL PRIMARY KEY,
    learner_id                     UUID        NOT NULL REFERENCES learners (id) ON DELETE CASCADE,
    entry_id                       UUID        NOT NULL REFERENCES dictionary_entries (id),
    status                         VARCHAR(16) NOT NULL,
    consecutive_scheduled_correct  SMALLINT    NOT NULL,
    first_shown_day                DATE        NULL,
    last_review_day                DATE        NULL,
    interval_days                  INTEGER     NULL,
    due_on                         DATE        NULL,
    control_pending                BOOLEAN     NOT NULL,
    lapses                         INTEGER     NOT NULL,
    model_state                    JSONB       NOT NULL,
    scheduler_version              VARCHAR(16) NOT NULL,
    created_at                     TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_learner_words_learner_entry ON learner_words (learner_id, entry_id);
CREATE INDEX IF NOT EXISTS ix_learner_words_learner_due ON learner_words (learner_id, due_on);
CREATE INDEX IF NOT EXISTS ix_learner_words_learner_status ON learner_words (learner_id, status);
CREATE INDEX IF NOT EXISTS ix_learner_words_entry_id ON learner_words (entry_id);
