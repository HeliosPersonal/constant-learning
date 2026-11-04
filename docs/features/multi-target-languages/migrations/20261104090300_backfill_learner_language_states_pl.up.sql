-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

INSERT INTO learner_language_states (
    learner_id, target_language, daily_new_words_limit, pending_daily_new_words_limit,
    pending_limit_effective_from, new_words_available_from, current_day_date,
    current_day_starts_at, current_day_ends_at, current_day_original_ends_at,
    current_day_time_zone_at_start, course_completed_at)
SELECT
    id, 'pl', daily_new_words_limit, pending_daily_new_words_limit,
    pending_limit_effective_from, new_words_available_from, current_day_date,
    current_day_starts_at, current_day_ends_at, current_day_original_ends_at,
    current_day_time_zone_at_start, course_completed_at
FROM learners
ON CONFLICT (learner_id, target_language) DO NOTHING;
