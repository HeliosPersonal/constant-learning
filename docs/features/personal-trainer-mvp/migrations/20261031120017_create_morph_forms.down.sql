-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_morph_forms_lang_lemma;
DROP TABLE IF EXISTS morph_forms;
