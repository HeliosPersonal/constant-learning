-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS version_translations (
    id          UUID         NOT NULL PRIMARY KEY,
    version_id  UUID         NOT NULL REFERENCES entry_versions (id) ON DELETE CASCADE,
    kind        VARCHAR(16)  NOT NULL,
    text_uk     VARCHAR(200) NOT NULL,
    position    SMALLINT     NOT NULL,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_version_translations_version_position ON version_translations (version_id, position);
