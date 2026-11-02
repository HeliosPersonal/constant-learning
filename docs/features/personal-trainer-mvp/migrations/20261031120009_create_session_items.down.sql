-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
DROP INDEX IF EXISTS ix_session_items_entry_id;
DROP INDEX IF EXISTS ux_session_items_session_position;
DROP TABLE IF EXISTS session_items;
