-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_answer_neutralizations_content_report_id;
DROP INDEX IF EXISTS ux_answer_neutralizations_answer_event;
DROP TABLE IF EXISTS answer_neutralizations;
