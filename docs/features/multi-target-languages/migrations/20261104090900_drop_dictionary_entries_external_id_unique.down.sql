-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

CREATE UNIQUE INDEX IF NOT EXISTS ux_dictionary_entries_external_id ON dictionary_entries (external_id);
