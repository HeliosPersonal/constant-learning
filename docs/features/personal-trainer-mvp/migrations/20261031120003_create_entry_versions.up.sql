-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS entry_versions (
    id                   UUID         NOT NULL PRIMARY KEY,
    entry_id             UUID         NOT NULL REFERENCES dictionary_entries (id) ON DELETE CASCADE,
    based_on_version_id  UUID         NULL REFERENCES entry_versions (id),
    lemma_pl             VARCHAR(200) NOT NULL,
    part_of_speech       VARCHAR(32)  NOT NULL,
    gender               VARCHAR(8)   NULL,
    gender_pair_id       VARCHAR(64)  NULL,
    aspect_pair_id       VARCHAR(64)  NULL,
    pronunciation_uk     VARCHAR(200) NOT NULL,
    example_pl           VARCHAR(500) NOT NULL,
    example_uk           VARCHAR(500) NOT NULL,
    show_context_before  BOOLEAN      NOT NULL,
    frequency_rank       INTEGER      NULL,
    utility_rank         INTEGER      NULL,
    legalization_stage   VARCHAR(32)  NULL,
    topic                VARCHAR(32)  NULL,
    validation_status    VARCHAR(16)  NOT NULL,
    created_by_kind      VARCHAR(24)  NOT NULL,
    content_report_id    UUID         NULL,
    content_hash         VARCHAR(64)  NOT NULL,
    review_note          VARCHAR(500) NULL,
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_entry_versions_entry_id ON entry_versions (entry_id, created_at);
CREATE INDEX IF NOT EXISTS ix_entry_versions_based_on_version_id ON entry_versions (based_on_version_id);
CREATE INDEX IF NOT EXISTS ix_entry_versions_content_report_id ON entry_versions (content_report_id);
