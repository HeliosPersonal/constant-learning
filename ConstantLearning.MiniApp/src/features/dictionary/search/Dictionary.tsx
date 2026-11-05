import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react';
import { BackButton } from '../../../components/BackButton';
import { ActiveLanguageBadge } from '../../../components/ActiveLanguageBadge';
import { Button, Card, Result, Spinner } from '../../../components/ui';
import type { Learner } from '../../onboarding/api';
import { DictionaryApiError, getDictionaryEntry, searchDictionary, type EntryCard, type MissingWordReportRequest, type SearchCursor } from './api';
import { useDictionaryResource } from './useDictionaryResource';
import type { ReportContext } from '../report/model';
import './dictionary.css';

const ContentReportDialog = lazy(() => import('../report/ContentReportDialog'));

const statuses = { not_started: 'Ще не вчив', review: 'На повторення', recalled: 'Вже пригадую' };
const parts: Record<string, string> = {
  noun: 'іменник', verb: 'дієслово', adj: 'прикметник', adv: 'прислівник', pron: 'займенник', det: 'означник',
  prep: 'прийменник', conj: 'сполучник', particle: 'частка', num: 'числівник', intj: 'вигук',
  adjective: 'прикметник', adverb: 'прислівник', pronoun: 'займенник', numeral: 'числівник', preposition: 'прийменник',
  conjunction: 'сполучник', interjection: 'вигук', phrase: 'словосполучення',
};
const genders: Record<string, string> = { m: 'чоловічий рід', f: 'жіночий рід', n: 'середній рід', pl: 'множина' };
const translationKinds = { canonical: 'Основний переклад', variant: 'Додатковий переклад', local: 'Місцевий варіант' };

function Failure({ error, onRetry, entry = false }: { error: unknown; onRetry: () => void; entry?: boolean }) {
  const unauthorized = error instanceof DictionaryApiError && error.status === 401;
  const missing = entry && error instanceof DictionaryApiError && error.status === 404;
  return <Card><Result tone="error">{unauthorized ? 'Відкрийте тренер знову в Telegram, щоб продовжити.' : missing ? 'Цей запис більше недоступний у словнику. Поверніться до результатів і оновіть пошук.' : 'Не вдалося завантажити дані. Ваш запит залишився на екрані.'}</Result>
    {!unauthorized && <Button mode="outline" size="l" stretched onClick={onRetry}>{missing ? 'Оновити пошук' : 'Спробувати ще раз'}</Button>}
  </Card>;
}

function WordCard({ card }: { card: EntryCard }) {
  return <>
    <div className="dictionary-word-meta"><span>{parts[card.part_of_speech] ?? card.part_of_speech}{card.gender ? ` · ${genders[card.gender] ?? card.gender}` : ''}</span><span className="dictionary-status">{statuses[card.word_status]}</span></div>
    <dl className="ios-group" aria-label="Переклади">{card.translations_uk.map((translation, index) => <div className="ios-row dictionary-translation" key={`${translation.kind}-${index}`}><dt>{translationKinds[translation.kind]}</dt><dd className="ios-value">{translation.text_uk}</dd></div>)}</dl>
    <div className="ios-group"><div className="ios-row"><h3 className="dictionary-row-title">Вимова</h3><p className="ios-value dictionary-row-value">{card.pronunciation_uk}</p></div></div>
    <h3 className="ios-caption dictionary-example-title">Приклад</h3>
    <div className="ios-group dictionary-example"><p lang="pl">{card.example_pl}</p><p className="caption">{card.example_uk}</p></div>
  </>;
}

function Entry({ entryId, onBack, onRefresh, onReport }: { entryId: string; onBack: () => void; onRefresh: () => void; onReport: (entry: EntryCard) => void }) {
  const [attempt, setAttempt] = useState(0);
  const heading = useRef<HTMLHeadingElement>(null);
  const load = useCallback((signal: AbortSignal) => getDictionaryEntry(entryId, signal), [entryId]);
  const resource = useDictionaryResource(`${entryId}:${attempt}`, load);
  useEffect(() => { heading.current?.focus(); }, [entryId]);
  return <section className="dictionary-entry" aria-labelledby="dictionary-entry-title">
    <BackButton className="dictionary-back" onBack={onBack}>← До результатів</BackButton>
    <h2 id="dictionary-entry-title" className="dictionary-hero" ref={heading} tabIndex={-1} lang={resource.status === 'ready' ? 'pl' : 'uk'}>{resource.status === 'ready' ? resource.data.lemma_pl : 'Слово зі словника'}</h2>
    {resource.status === 'pending' && <div className="loading-state" role="status"><span aria-hidden="true"><Spinner size="s" /></span> Завантажуємо картку…</div>}
    {resource.status === 'ready' && <>
      <WordCard card={resource.data} />
      <div className="ios-group"><button type="button" className="ios-row ios-plain ios-accent dictionary-report-action" onClick={() => onReport(resource.data)}>Повідомити про неточність</button></div>
    </>}
    {resource.status === 'error' && <Failure entry error={resource.error} onRetry={() => resource.error instanceof DictionaryApiError && resource.error.status === 404 ? onRefresh() : setAttempt(value => value + 1)} />}
  </section>;
}

export default function Dictionary({ onBack, onReportMissing, learner }: { onBack: () => void; onReportMissing?: (request: MissingWordReportRequest) => void; learner?: Learner }) {
  const [rawQuery, setRawQuery] = useState('');
  const [cursor, setCursor] = useState<SearchCursor>({});
  const [attempt, setAttempt] = useState(0);
  const [selected, setSelected] = useState<string | null>(null);
  const [reportContext, setReportContext] = useState<ReportContext | null>(null);
  const [reportOpen, setReportOpen] = useState(false);
  const resultsHeading = useRef<HTMLHeadingElement>(null);
  const key = JSON.stringify([rawQuery, cursor, attempt]);
  const load = useCallback((signal: AbortSignal) => searchDictionary(rawQuery, cursor, signal), [rawQuery, cursor]);
  const resource = useDictionaryResource(key, rawQuery.trim() ? load : null);
  function changeQuery(value: string) { setRawQuery(value); setCursor({}); setSelected(null); }
  function openReport(context: ReportContext) { setReportContext(context); setReportOpen(true); }
  function closeEntry() { setSelected(null); requestAnimationFrame(() => resultsHeading.current?.focus()); }
  function refreshSearch() { closeEntry(); setCursor({}); setAttempt(value => value + 1); }
  return <div className="dictionary">
    {!selected && <BackButton className="dictionary-navigation" navLabel="Навігація словника" onBack={onBack}>← До «Сьогодні»</BackButton>}
    {!selected && <>
      <header className="language-heading"><h1 className="ios-title">Словник</h1>
      {learner && <ActiveLanguageBadge learner={learner} />}</header>
      <div className="dictionary-search"><label className="sr-only" htmlFor="dictionary-query">Слово для пошуку</label>
        <input id="dictionary-query" type="search" autoComplete="off" spellCheck={false} maxLength={200} value={rawQuery} onChange={event => changeQuery(event.target.value)} placeholder="Слово або переклад" />
        {rawQuery && <button type="button" className="dictionary-clear ios-plain" aria-label="Очистити" onClick={() => changeQuery('')}>×</button>}</div>
    </>}
    {selected ? <Entry entryId={selected} onBack={closeEntry} onRefresh={refreshSearch} onReport={entry => openReport({ origin: 'card', entry })} /> : <>
      <div role="status" aria-live="polite" className="dictionary-announcement">
        {resource.status === 'pending' && <div className="loading-state"><span aria-hidden="true"><Spinner size="s" /></span> Шукаємо слово…</div>}
        {resource.status === 'ready' && resource.data.items.length === 0 && <span className="sr-only">Не знайшли слово.</span>}
      </div>
      {resource.status === 'idle' && <div className="dictionary-empty"><h2>З чого почнемо?</h2><p className="caption">Введіть слово або його форму. Діакритика не обов’язкова, результати з’являтимуться після кожного символу.</p></div>}
      {resource.status === 'error' && <Failure error={resource.error} onRetry={() => setAttempt(value => value + 1)} />}
      {resource.status === 'ready' && (resource.data.items.length === 0
        ? <div className="dictionary-empty" aria-labelledby="dictionary-empty-title"><h2 id="dictionary-empty-title">Не знайшли слово</h2><p className="dictionary-query-quote">«{rawQuery}»</p><p className="caption">Перевірте написання або повідомте про відсутній запис.</p>
          <button type="button" className="dictionary-action ios-plain" onClick={() => { const context: MissingWordReportRequest = { origin: 'missing_word', search_query: rawQuery }; onReportMissing?.(context); openReport(context); }}>Повідомити про відсутній запис</button></div>
        : <section aria-labelledby="dictionary-results-title"><h2 id="dictionary-results-title" className="sr-only" ref={resultsHeading} tabIndex={-1}>Результати</h2>
          <ul className="ios-group dictionary-results">{resource.data.items.map(item => <li key={item.entry_id}><button type="button" className="ios-row ios-plain dictionary-result" onClick={() => setSelected(item.entry_id)}>
            <span className="dictionary-result-main"><strong className="dictionary-lemma" lang="pl">{item.lemma_pl}<span className={`dictionary-dot dictionary-dot--${item.word_status}`} aria-hidden="true" /></strong>
              <span className="dictionary-result-sub">{parts[item.part_of_speech] ?? item.part_of_speech} · {item.short_meaning_uk}</span>
              {item.matched_form !== item.lemma_pl && <span className="dictionary-match">Збіг: {item.matched_form}</span>}
              <span className="sr-only">{statuses[item.word_status]}</span></span>
            <span className="ios-value ios-chevron" aria-hidden="true">›</span>
          </button></li>)}</ul>
          {(resource.data.has_prev || resource.data.has_next) && <nav className="dictionary-pagination" aria-label="Сторінки результатів"><button type="button" className="ios-plain" disabled={!resource.data.has_prev} onClick={() => setCursor({ before: resource.data.items[0].entry_id })}>‹ Попередні</button><button type="button" className="ios-plain" disabled={!resource.data.has_next || !resource.data.next_cursor} onClick={() => setCursor({ after: resource.data.next_cursor! })}>Наступні ›</button></nav>}
        </section>)}
    </>}
    {reportContext && <Suspense fallback={reportOpen ? <p role="status">Відкриваємо форму повідомлення…</p> : null}><ContentReportDialog key={reportContext.origin === 'card' ? reportContext.entry.entry_id : `missing:${reportContext.search_query}`} open={reportOpen} context={reportContext} onClose={() => setReportOpen(false)} /></Suspense>}
  </div>;
}
