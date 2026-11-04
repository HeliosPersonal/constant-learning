-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

CREATE INDEX IF NOT EXISTS ix_dictionary_entries_course_position ON dictionary_entries (course_position) WHERE published_version_id IS NOT NULL;
