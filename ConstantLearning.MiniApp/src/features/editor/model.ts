import { EditorApiError, type AppliedChange, type Decision, type QueueItem, type RejectionReason } from './api';

export const fieldLabels: Record<AppliedChange['field'], string> = {
  lemma: 'Польське слово', translation: 'Переклад', pronunciation: 'Транскрипція',
  example: 'Приклад польською', accepted_answer: 'Прийнятий варіант відповіді',
};
export const originLabels: Record<QueueItem['origin'], string> = {
  card: 'Неточність у картці', answer_dispute: 'Моя відповідь теж правильна', missing_word: 'Відсутнє слово',
};
const reasonLabels: Record<RejectionReason, string> = {
  source_not_confirming: 'Джерело не підтверджує', already_correct: 'Запис уже правильний',
  out_of_scope: 'Поза межами словника', duplicate: 'Дублікат',
  already_in_dictionary: 'Уже є в словнику', phrase_not_lemma: 'Фраза, а не лема',
};
export function rejectionReasons(origin: QueueItem['origin']) {
  const values: RejectionReason[] = origin === 'missing_word'
    ? ['already_in_dictionary', 'phrase_not_lemma', 'out_of_scope']
    : ['source_not_confirming', 'already_correct', 'out_of_scope', 'duplicate'];
  return values.map(value => ({ value, label: value === 'out_of_scope' && origin === 'missing_word' ? 'Поза межами курсу' : reasonLabels[value] }));
}
export function queueField(item: QueueItem) { return item.origin === 'answer_dispute' ? fieldLabels.accepted_answer : item.field ? fieldLabels[item.field] : 'Кандидат до словника'; }
export function previousValue(change: AppliedChange) { return change.previous_value ?? (change.field === 'accepted_answer' ? 'Варіанта ще не було' : 'Немає попереднього значення'); }
export function editorDate(value: string) {
  return `${new Intl.DateTimeFormat('uk-UA', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }).format(new Date(value))} UTC`;
}
export type DecisionDraft = { decision: Decision['decision']; corrected: string; reason: RejectionReason | '' };
export type DecisionErrors = Partial<Record<'corrected' | 'reason' | 'general', string>>;
export function initialDraft(item: QueueItem): DecisionDraft { return { decision: item.origin === 'missing_word' ? 'add_to_candidates' : 'approve', corrected: '', reason: '' }; }

export function validateDecision(item: QueueItem, draft: DecisionDraft): DecisionErrors {
  if (draft.decision === 'reject') return rejectionReasons(item.origin).some(reason => reason.value === draft.reason) ? {} : { reason: 'Виберіть причину відхилення.' };
  if ((item.origin === 'missing_word') !== (draft.decision === 'add_to_candidates')) return { general: 'Ця дія не відповідає типу повідомлення.' };
  const source = item.origin === 'missing_word' ? item.search_query : item.proposed_value;
  const raw = draft.corrected || source || '';
  if (/\p{Cc}/u.test(raw)) return { corrected: 'Приберіть переноси рядка й керівні символи з остаточного значення.' };
  let value = raw.trim().normalize('NFC');
  const maximum = item.field === 'example' && item.origin !== 'missing_word' ? 500 : 200;
  if (item.origin === 'missing_word') value = value.toLowerCase();
  if (!value) return { corrected: 'Вкажіть остаточне значення: у повідомленні немає пропозиції.' };
  if (value.length > maximum || (draft.corrected && Array.from(draft.corrected).length > 500)) return { corrected: `Остаточне значення може містити щонайбільше ${maximum} символів.` };
  const field = item.origin === 'missing_word' ? 'lemma' : item.origin === 'answer_dispute' ? 'accepted_answer' : item.field;
  if (field === 'lemma' && !/^[a-ząćęłńóśźż]+$/.test(value)) return { corrected: 'Вкажіть одну польську лему малими літерами, без пробілів.' };
  if (field === 'accepted_answer' && !/^[a-ząćęłńóśźż]+(?:[ '-][a-ząćęłńóśźż]+)*$/.test(value)) return { corrected: 'Вкажіть польський варіант відповіді малими літерами.' };
  if ((field === 'translation' || field === 'pronunciation') && (!/[а-яіїєґА-ЯІЇЄҐ]/.test(value) || value.includes(';'))) return { corrected: 'Вкажіть одне українське значення без крапки з комою.' };
  if (field === 'pronunciation' && /[a-zA-Z]/.test(value)) return { corrected: 'Запишіть транскрипцію українськими літерами.' };
  if (field === 'example' && (!/[a-ząćęłńóśźżA-ZĄĆĘŁŃÓŚŹŻ]/.test(value) || /[а-яіїєґА-ЯІЇЄҐ]/.test(value))) return { corrected: 'Запишіть приклад польською мовою.' };
  return {};
}
export function decisionBody(draft: DecisionDraft): Decision {
  return draft.decision === 'reject' ? { decision: 'reject', rejection_reason: draft.reason as RejectionReason }
    : { decision: draft.decision, ...(draft.corrected ? { corrected_value: draft.corrected } : {}) };
}
export function accessFailure(error: unknown): 'auth' | 'role' | null {
  return error instanceof EditorApiError ? error.status === 401 ? 'auth' : error.status === 403 ? 'role' : null : null;
}
export function decisionFailure(error: unknown): string {
  if (!(error instanceof EditorApiError)) return 'Не вдалося зберегти рішення. Ваш вибір залишився на екрані — спробуйте ще раз.';
  const messages: Record<string, string> = {
    'editor.rejection_reason_required': 'Виберіть причину відхилення.',
    'editor.invalid_rejection_reason': 'Виберіть причину, яка відповідає цьому повідомленню.',
    'editor.invalid_value': 'Остаточне значення не відповідає полю словника. Перевірте введення.',
    'editor.duplicate_lemma': 'Така лема вже є у словнику або кандидатах.',
    'editor.value_already_present': 'Запис уже містить це значення.',
    'editor.approval_requirements_not_met': 'Запис не пройшов усі перевірки для публікації.',
    'editor.published_version_changed': 'Опублікована версія змінилася. Оновіть чергу й перевірте актуальний запис.',
    'report.not_in_editor_queue': 'Повідомлення ще проходить автоматичну звірку.',
    'report.not_found': 'Повідомлення більше недоступне. Чергу оновлено.',
    'report.already_closed': 'Це повідомлення вже розглянуто. Чергу оновлено.',
  };
  return messages[error.body?.code ?? ''] ?? 'Не вдалося зберегти рішення. Ваш вибір залишився на екрані — спробуйте ще раз.';
}
