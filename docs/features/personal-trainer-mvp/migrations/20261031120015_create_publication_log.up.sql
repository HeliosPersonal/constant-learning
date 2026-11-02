-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS publication_log (
    id                UUID        NOT NULL PRIMARY KEY,
    entry_id          UUID        NOT NULL REFERENCES dictionary_entries (id) ON DELETE CASCADE,
    from_version_id   UUID        NULL REFERENCES entry_versions (id),
    to_version_id     UUID        NULL REFERENCES entry_versions (id),
    action            VARCHAR(16) NOT NULL,
    actor_kind        VARCHAR(24) NOT NULL,
    actor_learner_id  UUID        NULL REFERENCES learners (id),
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_publication_log_entry_created ON publication_log (entry_id, created_at);
CREATE INDEX IF NOT EXISTS ix_publication_log_from_version_id ON publication_log (from_version_id);
CREATE INDEX IF NOT EXISTS ix_publication_log_to_version_id ON publication_log (to_version_id);
CREATE INDEX IF NOT EXISTS ix_publication_log_actor_learner_id ON publication_log (actor_learner_id);
