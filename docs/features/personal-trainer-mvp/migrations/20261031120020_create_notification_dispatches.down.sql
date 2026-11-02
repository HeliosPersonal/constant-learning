-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_notification_dispatches_learner_id;
DROP INDEX IF EXISTS ix_notification_dispatches_status_due;
DROP INDEX IF EXISTS ux_notification_dispatches_recipient_kind_date;
DROP TABLE IF EXISTS notification_dispatches;
