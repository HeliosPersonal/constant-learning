-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

DROP INDEX IF EXISTS ux_dictionary_entries_external_id;
