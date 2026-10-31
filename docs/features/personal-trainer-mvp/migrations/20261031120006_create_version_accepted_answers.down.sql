-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ux_version_accepted_answers_version_text;
DROP TABLE IF EXISTS version_accepted_answers;
