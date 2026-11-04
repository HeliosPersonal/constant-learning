-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

ALTER TABLE learners ADD COLUMN IF NOT EXISTS active_target_language VARCHAR(2) NULL REFERENCES target_languages (code);
UPDATE learners SET active_target_language = 'pl' WHERE active_target_language IS NULL;
