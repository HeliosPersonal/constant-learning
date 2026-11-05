import type { components } from '../../../api/schema';
import type { EntryCard, MissingWordReportRequest } from '../search/api';

export type ReportContext = { origin: 'card'; entry: EntryCard } | MissingWordReportRequest;
export type ReportField = components['schemas']['ReportField'];
export type ReportDraft = { field: ReportField | ''; proposed: string; comment: string };
export type ReportErrors = Partial<Record<'field' | 'proposed' | 'comment' | 'query' | 'general', string>>;
export const reportFields: { value: ReportField; label: string }[] = [
  { value: 'lemma', label: 'Слово' }, { value: 'translation', label: 'Переклад' },
  { value: 'pronunciation', label: 'Транскрипція' }, { value: 'example', label: 'Приклад' },
];

export function textLength(value: string) { return Array.from(value).length; }
// Набір пробілів відповідає .NET String.IsNullOrWhiteSpace, включно з U+0085.
function isBlank(value: string) { return /^[\t-\r\u0085\p{Zs}\p{Zl}\p{Zp}]*$/u.test(value); }

export function validateReport(context: ReportContext, draft: ReportDraft): ReportErrors {
  const errors: ReportErrors = {};
  if (context.origin === 'card' && !reportFields.some(field => field.value === draft.field)) errors.field = 'Виберіть поле, якого стосується повідомлення.';
  if (textLength(draft.proposed) > 500) errors.proposed = 'Запропоноване значення може містити щонайбільше 500 символів.';
  else if (draft.proposed.includes('\0')) errors.proposed = 'Приберіть нульовий символ із запропонованого значення.';
  if (textLength(draft.comment) > 1000) errors.comment = 'Коментар може містити щонайбільше 1000 символів.';
  else if (draft.comment.includes('\0')) errors.comment = 'Приберіть нульовий символ із коментаря.';
  if (context.origin === 'missing_word' && (isBlank(context.search_query) || textLength(context.search_query) > 200 || context.search_query.includes('\0'))) errors.query = 'Пошуковий запит має містити від 1 до 200 символів без нульового символу. Поверніться до словника й уточніть запит.';
  return errors;
}

export function reportBody(context: ReportContext, draft: ReportDraft): components['schemas']['ContentReportCreate'] {
  return {
    origin: context.origin,
    entry_id: context.origin === 'card' ? context.entry.entry_id : null,
    field: context.origin === 'card' ? draft.field as ReportField : null,
    proposed_value: isBlank(draft.proposed) ? null : draft.proposed,
    comment: draft.comment || null,
    search_query: context.origin === 'missing_word' ? context.search_query : null,
  };
}
