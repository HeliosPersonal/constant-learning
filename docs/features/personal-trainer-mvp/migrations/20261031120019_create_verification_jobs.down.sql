-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_verification_jobs_status_next_attempt;
DROP INDEX IF EXISTS ux_verification_jobs_content_report;
DROP TABLE IF EXISTS verification_jobs;
