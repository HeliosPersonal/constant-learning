-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_dictionary_entries_published_version_id;
ALTER TABLE dictionary_entries DROP CONSTRAINT IF EXISTS fk_dictionary_entries_published_version;
