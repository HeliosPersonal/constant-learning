-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

ALTER TABLE learning_sessions ADD COLUMN IF NOT EXISTS target_language VARCHAR(2) NULL REFERENCES target_languages (code);
UPDATE learning_sessions SET target_language = 'pl' WHERE target_language IS NULL;
