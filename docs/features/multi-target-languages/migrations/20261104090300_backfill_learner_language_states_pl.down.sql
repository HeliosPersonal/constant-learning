-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

DELETE FROM learner_language_states WHERE target_language = 'pl';
