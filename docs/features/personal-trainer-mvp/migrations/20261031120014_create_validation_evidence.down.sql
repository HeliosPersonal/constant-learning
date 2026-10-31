-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_validation_evidence_version_id;
DROP TABLE IF EXISTS validation_evidence;
