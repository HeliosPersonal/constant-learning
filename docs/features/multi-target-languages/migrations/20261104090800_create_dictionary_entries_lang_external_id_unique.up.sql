-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
-- CONCURRENTLY: один statement, без транзакції. Промоушен: migrationBuilder.Sql(..., suppressTransaction: true).

CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_dictionary_entries_lang_external_id ON dictionary_entries (target_language, external_id);
