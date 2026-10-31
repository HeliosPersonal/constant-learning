-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
CREATE TABLE IF NOT EXISTS morph_forms (
    id          UUID         NOT NULL PRIMARY KEY,
    lang        VARCHAR(2)   NOT NULL,
    source      VARCHAR(32)  NOT NULL,
    lemma       VARCHAR(200) NOT NULL,
    form        VARCHAR(200) NOT NULL,
    tags        VARCHAR(64)  NULL,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_morph_forms_lang_lemma ON morph_forms (lang, lemma);
