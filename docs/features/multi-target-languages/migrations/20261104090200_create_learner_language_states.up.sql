-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

CREATE TABLE IF NOT EXISTS learner_language_states (
    learner_id                     UUID        NOT NULL REFERENCES learners (id) ON DELETE CASCADE,
    target_language                VARCHAR(2)  NOT NULL REFERENCES target_languages (code),
    daily_new_words_limit          SMALLINT    NOT NULL,
    pending_daily_new_words_limit  SMALLINT    NULL,
    pending_limit_effective_from   TIMESTAMPTZ NULL,
    new_words_available_from       TIMESTAMPTZ NULL,
    current_day_date               DATE        NULL,
    current_day_starts_at          TIMESTAMPTZ NULL,
    current_day_ends_at            TIMESTAMPTZ NULL,
    current_day_original_ends_at   TIMESTAMPTZ NULL,
    current_day_time_zone_at_start VARCHAR(64) NULL,
    course_completed_at            TIMESTAMPTZ NULL,
    created_at                     TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (learner_id, target_language)
);
CREATE INDEX IF NOT EXISTS ix_learner_language_states_target_language ON learner_language_states (target_language);
