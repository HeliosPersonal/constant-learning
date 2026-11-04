-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

ALTER TABLE answer_events DROP COLUMN IF EXISTS target_language;
