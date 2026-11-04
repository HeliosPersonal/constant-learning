-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

ALTER TABLE dictionary_entries ALTER COLUMN target_language SET NOT NULL;
