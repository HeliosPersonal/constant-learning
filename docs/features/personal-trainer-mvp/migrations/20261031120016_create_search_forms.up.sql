-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS search_forms (
    id            UUID         NOT NULL PRIMARY KEY,
    entry_id      UUID         NOT NULL REFERENCES dictionary_entries (id) ON DELETE CASCADE,
    version_id    UUID         NOT NULL REFERENCES entry_versions (id) ON DELETE CASCADE,
    lang          VARCHAR(2)   NOT NULL,
    form_display  VARCHAR(200) NOT NULL,
    form_folded   VARCHAR(200) NOT NULL,
    is_lemma      BOOLEAN      NOT NULL,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_search_forms_form_folded ON search_forms (form_folded text_pattern_ops);
CREATE INDEX IF NOT EXISTS ix_search_forms_entry_id ON search_forms (entry_id);
CREATE INDEX IF NOT EXISTS ix_search_forms_version_id ON search_forms (version_id);
