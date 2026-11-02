-- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
-- Contract-фаза ADR-0004: застосовувати окремим релізом ПІСЛЯ видалення групового коду й після pg_dump.
DROP TABLE IF EXISTS "LearnedWords";
DROP TABLE IF EXISTS "ChatRegistrations";
DROP TABLE IF EXISTS "Words";
