-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ux_learners_reporter_alias;
DROP INDEX IF EXISTS ux_learners_telegram_user_id;
DROP TABLE IF EXISTS learners;
