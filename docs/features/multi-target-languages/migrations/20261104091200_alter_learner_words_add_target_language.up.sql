-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

ALTER TABLE learner_words ADD COLUMN IF NOT EXISTS target_language VARCHAR(2) NULL REFERENCES target_languages (code);
UPDATE learner_words lw SET target_language = de.target_language
FROM dictionary_entries de
WHERE de.id = lw.entry_id AND lw.target_language IS NULL;
