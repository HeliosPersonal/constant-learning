-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

DELETE FROM target_languages WHERE code IN ('pl', 'en', 'de', 'nl');
