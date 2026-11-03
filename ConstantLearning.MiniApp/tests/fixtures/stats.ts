import type { LearnedWordCard, LearnedWordStat, LearnedWordsPage, LearnedWordsSummary } from '../../src/features/stats/api';

export const summary: LearnedWordsSummary = {
  seen_count: 42, recalled_count: 18, review_count: 24, weak_count: 6,
};

export const weakWord: LearnedWordStat = {
  entry_id: '0192f0a0-0000-7000-8000-0000000000e1', lemma_pl: 'chata', transcription_uk: 'хата',
  translation_uk: 'хата', status: 'review', shown_count: 7, correct_count: 4, incorrect_count: 3,
  streak: 1, forgotten_count: 2, first_shown_day: '2026-10-20', last_review_day: '2026-10-31',
  due_on: '2026-11-02', is_weak: true,
};

export const strongWord: LearnedWordStat = {
  entry_id: '0192f0a0-0000-7000-8000-0000000000e2', lemma_pl: 'woda', transcription_uk: null,
  translation_uk: 'вода', status: 'recalled', shown_count: 10, correct_count: 10, incorrect_count: 0,
  streak: 5, forgotten_count: 0, first_shown_day: '2026-10-10', last_review_day: '2026-10-30',
  due_on: '2026-11-20', is_weak: false,
};

export const wordsPage: LearnedWordsPage = {
  items: [weakWord, strongWord], has_next: false, has_prev: false, next_cursor: null,
};

export const wordCard: LearnedWordCard = {
  ...weakWord,
  translations_uk: [{ kind: 'canonical', text_uk: 'хата' }, { kind: 'local', text_uk: 'хатка' }],
  mistake_breakdown: { typo: 1, diacritic: 1, dont_know: 1 },
};
