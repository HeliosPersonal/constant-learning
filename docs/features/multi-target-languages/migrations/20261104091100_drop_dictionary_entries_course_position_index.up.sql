-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

DROP INDEX IF EXISTS ix_dictionary_entries_course_position;
