-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

ALTER TABLE content_reports ADD COLUMN IF NOT EXISTS target_language VARCHAR(2) NULL REFERENCES target_languages (code);
UPDATE content_reports SET target_language = 'pl' WHERE target_language IS NULL;
