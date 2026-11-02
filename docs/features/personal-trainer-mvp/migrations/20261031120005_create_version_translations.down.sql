-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ux_version_translations_version_position;
DROP TABLE IF EXISTS version_translations;
