-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS learners (
    id                            UUID         NOT NULL PRIMARY KEY,
    telegram_user_id              BIGINT       NOT NULL,
    first_name                    VARCHAR(64)  NULL,
    time_zone                     VARCHAR(64)  NOT NULL,
    time_zone_confirmed           BOOLEAN      NOT NULL,
    daily_new_words_limit         SMALLINT     NOT NULL,
    pending_daily_new_words_limit SMALLINT     NULL,
    pending_limit_effective_from  TIMESTAMPTZ  NULL,
    new_words_available_from      TIMESTAMPTZ  NULL,
    reporter_alias                VARCHAR(16)  NULL,
    can_message                   BOOLEAN      NOT NULL,
    onboarded_at                  TIMESTAMPTZ  NULL,
    created_at                    TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_learners_telegram_user_id ON learners (telegram_user_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_learners_reporter_alias ON learners (reporter_alias);
