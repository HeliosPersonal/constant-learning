-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_dictionary_entries_course_position;
DROP INDEX IF EXISTS ux_dictionary_entries_external_id;
DROP TABLE IF EXISTS dictionary_entries;
