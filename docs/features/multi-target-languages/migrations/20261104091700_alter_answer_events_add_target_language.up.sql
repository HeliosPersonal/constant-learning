-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

ALTER TABLE answer_events ADD COLUMN IF NOT EXISTS target_language VARCHAR(2) NULL REFERENCES target_languages (code);
UPDATE answer_events ae SET target_language = de.target_language
FROM dictionary_entries de
WHERE de.id = ae.entry_id AND ae.target_language IS NULL;
