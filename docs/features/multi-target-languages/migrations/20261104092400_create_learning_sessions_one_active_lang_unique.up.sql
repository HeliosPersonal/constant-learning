-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
-- CONCURRENTLY: один statement, без транзакції. Промоушен: migrationBuilder.Sql(..., suppressTransaction: true).
-- Критик #1 / AC-04: мово-свідомий UNIQUE замінює глобальний ux_learning_sessions_one_active (знімається наступним кроком 20261104092450).
-- Служить і обмеженням «одна активна session НА МОВУ», і lookup активної session (learner_id, target_language) WHERE status='active'.

CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_learning_sessions_one_active_lang ON learning_sessions (learner_id, target_language) WHERE status = 'active';
