-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS validation_evidence (
    id            UUID         NOT NULL PRIMARY KEY,
    version_id    UUID         NOT NULL REFERENCES entry_versions (id) ON DELETE CASCADE,
    field         VARCHAR(32)  NOT NULL,
    source        VARCHAR(32)  NOT NULL,
    url           TEXT         NOT NULL,
    revision_id   VARCHAR(32)  NULL,
    attribution   VARCHAR(200) NULL,
    outcome       VARCHAR(16)  NOT NULL,
    retrieved_at  TIMESTAMPTZ  NOT NULL,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_validation_evidence_version_id ON validation_evidence (version_id);
