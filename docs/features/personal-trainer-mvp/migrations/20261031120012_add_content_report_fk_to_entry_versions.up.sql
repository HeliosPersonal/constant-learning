-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
ALTER TABLE entry_versions
    ADD CONSTRAINT fk_entry_versions_content_report
    FOREIGN KEY (content_report_id) REFERENCES content_reports (id);
