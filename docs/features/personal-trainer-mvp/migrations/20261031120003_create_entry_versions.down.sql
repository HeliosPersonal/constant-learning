-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_entry_versions_content_report_id;
DROP INDEX IF EXISTS ix_entry_versions_based_on_version_id;
DROP INDEX IF EXISTS ix_entry_versions_entry_id;
DROP TABLE IF EXISTS entry_versions;
