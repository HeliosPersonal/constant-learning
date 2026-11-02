-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_publication_log_actor_learner_id;
DROP INDEX IF EXISTS ix_publication_log_to_version_id;
DROP INDEX IF EXISTS ix_publication_log_from_version_id;
DROP INDEX IF EXISTS ix_publication_log_entry_created;
DROP TABLE IF EXISTS publication_log;
