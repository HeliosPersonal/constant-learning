-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
ALTER TABLE entry_versions DROP CONSTRAINT IF EXISTS fk_entry_versions_content_report;
