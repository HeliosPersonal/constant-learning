import type { TodaySummary } from '../../src/features/today/api';

export const todaySummary: TodaySummary = {
  learner_day: '2026-11-02', due_review_count: 8, recalled_words_count: 12,
  completed_sessions_last_7_days: 3, has_active_session: false,
  course_state: 'in_progress', course_replenished_notice: false, new_words_available_today: true,
  extra_actions: {
    plan_done: false,
    extra_new_word: { available: false, reason: 'plan_not_done' },
    extra_practice: { available: false, reason: 'plan_not_done' },
  },
};

export const planDoneSummary: TodaySummary = {
  ...todaySummary, due_review_count: 0, new_words_available_today: false,
  extra_actions: {
    plan_done: true,
    extra_new_word: { available: true, reason: null },
    extra_practice: { available: true, reason: null },
  },
};
