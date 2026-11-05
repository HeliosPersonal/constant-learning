import type { AppliedChange, AppliedPage, QueueItem, QueuePage } from '../../src/features/editor/api';

export const report: QueueItem = {
  id: '0192f0a0-0000-7000-8000-0000000000d1', target_language: 'pl', reporter_alias: 'R-7F3A9C', origin: 'card',
  entry_id: '0192f0a0-0000-7000-8000-0000000000e1', lemma: 'urząd', field: 'translation',
  current_value: 'державна установа', proposed_value: 'установа', comment: 'Перевірте основний переклад.',
  search_query: null, created_at: '2026-11-02T10:00:00Z',
};
export const missingReport: QueueItem = { ...report, id: '0192f0a0-0000-7000-8000-0000000000d2', reporter_alias: 'R-2B4C6D', origin: 'missing_word', entry_id: null, lemma: null, field: null, current_value: null, proposed_value: null, search_query: 'довідка', comment: 'Не знаходжу потрібного слова.' };
export const disputeReport: QueueItem = { ...report, id: '0192f0a0-0000-7000-8000-0000000000d3', reporter_alias: 'R-8E6F4A', origin: 'answer_dispute', field: null, current_value: null, proposed_value: 'urzędzie', comment: 'Ця форма відповідає контексту вправи.' };
export const queuePage: QueuePage = { items: [report], has_next: false, has_prev: false, next_cursor: null };
export const change: AppliedChange = {
  id: '0192f0a0-0000-7000-8000-0000000000f1', entry_id: report.entry_id!, lemma_pl: 'urząd', field: 'translation',
  previous_value: 'державна установа', new_value: 'установа', confirmed_by: 'auto_verification',
  content_report_id: report.id, applied_at: '2026-11-02T11:00:00Z', rollback_allowed: true,
};
export const answerChange: AppliedChange = { ...change, id: '0192f0a0-0000-7000-8000-0000000000f2', field: 'accepted_answer', previous_value: null, new_value: 'urzędzie', confirmed_by: 'editor', rollback_allowed: false };
export const appliedPage: AppliedPage = { items: [change, answerChange], has_next: false, has_prev: false, next_cursor: null };
export function closedReport(id: string, outcome: 'applied' | 'added_to_candidates' | 'rejected' = 'applied', rejection_reason: string | null = null) {
  return { id, status: 'closed', outcome, rejection_reason, resulting_version_id: outcome === 'rejected' ? null : '0192f0a0-0000-7000-8000-0000000000a1', closed_at: '2026-11-02T12:00:00Z' };
}
export const rollbackResult = { change_id: change.id, entry_id: change.entry_id, published_version_id: '0192f0a0-0000-7000-8000-0000000000a0', rolled_back_at: '2026-11-02T13:00:00Z' };
