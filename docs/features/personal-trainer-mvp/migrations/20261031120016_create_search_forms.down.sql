-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_search_forms_version_id;
DROP INDEX IF EXISTS ix_search_forms_entry_id;
DROP INDEX IF EXISTS ix_search_forms_form_folded;
DROP TABLE IF EXISTS search_forms;
