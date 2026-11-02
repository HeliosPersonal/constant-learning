import { useCallback, useEffect, useRef, useState } from 'react';
import { Button, Card, Result, Spinner } from '../../../components/ui';
import { DictionaryApiError, getDictionaryEntry, searchDictionary, type EntryCard, type MissingWordReportRequest, type SearchCursor } from './api';
import { useDictionaryResource } from './useDictionaryResource';
import './dictionary.css';

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
    <dl className="dictionary-translations">{card.translations_uk.map((translation, index) => <div key={`${translation.kind}-${index}`}><dt>{translationKinds[translation.kind]}</dt><dd>{translation.text_uk}</dd></div>)}</dl>
    <div className="dictionary-pronunciation"><h3>Вимова</h3><p>{card.pronunciation_uk}</p></div>
    <div className="dictionary-example"><h3>Приклад</h3><p lang="pl">{card.example_pl}</p><p className="caption">{card.example_uk}</p></div>
    <p className="caption dictionary-footnote">Перегляд слова не змінює порядок слів у заняттях.</p>
  </>;
}

function Entry({ entryId, onBack, onRefresh }: { entryId: string; onBack: () => void; onRefresh: () => void }) {
  const [attempt, setAttempt] = useState(0);
  const heading = useRef<HTMLHeadingElement>(null);
  const load = useCallback((signal: AbortSignal) => getDictionaryEntry(entryId, signal), [entryId]);
  const resource = useDictionaryResource(`${entryId}:${attempt}`, load);
  useEffect(() => { heading.current?.focus(); }, [entryId]);
  return <div className="dictionary-entry">
    <Button mode="plain" size="l" onClick={onBack}>← До результатів</Button>
    <Card aria-labelledby="dictionary-entry-title">
      <p className="eyebrow">КАРТКА СЛОВА</p>
      <h2 id="dictionary-entry-title" ref={heading} tabIndex={-1} lang={resource.status === 'ready' ? 'pl' : 'uk'}>{resource.status === 'ready' ? resource.data.lemma_pl : 'Слово зі словника'}</h2>
      {resource.status === 'pending' && <div className="loading-state" role="status"><span aria-hidden="true"><Spinner size="s" /></span> Завантажуємо картку…</div>}
      {resource.status === 'ready' && <WordCard card={resource.data} />}
    </Card>
    {resource.status === 'error' && <Failure entry error={resource.error} onRetry={() => resource.error instanceof DictionaryApiError && resource.error.status === 404 ? onRefresh() : setAttempt(value => value + 1)} />}
  </div>;
}

export default function Dictionary({ onBack, onReportMissing }: { onBack: () => void; onReportMissing?: (request: MissingWordReportRequest) => void }) {
  const [rawQuery, setRawQuery] = useState('');
  const [cursor, setCursor] = useState<SearchCursor>({});
  const [attempt, setAttempt] = useState(0);
  const [selected, setSelected] = useState<string | null>(null);
  const [reportUnavailable, setReportUnavailable] = useState(false);
  const resultsHeading = useRef<HTMLHeadingElement>(null);
  const key = JSON.stringify([rawQuery, cursor, attempt]);
  const load = useCallback((signal: AbortSignal) => searchDictionary(rawQuery, cursor, signal), [rawQuery, cursor]);
  const resource = useDictionaryResource(key, rawQuery.trim() ? load : null);
  function changeQuery(value: string) { setRawQuery(value); setCursor({}); setSelected(null); setReportUnavailable(false); }
  function closeEntry() { setSelected(null); requestAnimationFrame(() => resultsHeading.current?.focus()); }
  function refreshSearch() { closeEntry(); setCursor({}); setAttempt(value => value + 1); }
  return <div className="dictionary">
    <nav className="dictionary-navigation" aria-label="Навігація словника"><Button mode="plain" size="l" onClick={onBack}>← До «Сьогодні»</Button></nav>
    <div className="intro"><p className="eyebrow">ЗНАЙДІТЬ ПОТРІБНЕ СЛОВО</p><h1>Словник</h1><p>Польське слово, український переклад або знайома форма.</p></div>
    <div className="dictionary-search"><label htmlFor="dictionary-query">Слово для пошуку</label><div className="dictionary-input-row"><input id="dictionary-query" type="search" autoComplete="off" spellCheck={false} maxLength={200} aria-describedby="dictionary-search-hint" value={rawQuery} onChange={event => changeQuery(event.target.value)} placeholder="Наприклад, urzedzie або установа" />{rawQuery && <Button mode="outline" size="l" onClick={() => changeQuery('')}>Очистити</Button>}</div>
      <p id="dictionary-search-hint" className="caption">Можна без діакритики. Один символ — точний збіг, від двох — також початок слова.</p></div>
    {selected ? <Entry entryId={selected} onBack={closeEntry} onRefresh={refreshSearch} /> : <>
      <div role="status" aria-live="polite" className="dictionary-announcement">
        {resource.status === 'pending' && <div className="loading-state"><span aria-hidden="true"><Spinner size="s" /></span> Шукаємо слово…</div>}
        {resource.status === 'ready' && resource.data.items.length > 0 && <p className="caption">На цій сторінці: {resource.data.items.length}. Точний збіг — першим.</p>}
        {resource.status === 'ready' && resource.data.items.length === 0 && <span className="sr-only">Не знайшли слово.</span>}
      </div>
      {resource.status === 'idle' && <Card><h2>З чого почнемо?</h2><p className="caption">Введіть слово або його форму. Результати з’являтимуться після кожного символу.</p></Card>}
      {resource.status === 'error' && <Failure error={resource.error} onRetry={() => setAttempt(value => value + 1)} />}
      {resource.status === 'ready' && (resource.data.items.length === 0 ? <Card aria-labelledby="dictionary-empty-title"><h2 id="dictionary-empty-title">Не знайшли слово</h2><p className="dictionary-query-quote">«{rawQuery}»</p><p className="caption">Перевірте написання або повідомте про відсутній запис.</p><Button className="dictionary-report-action" mode="outline" size="l" stretched onClick={() => { if (onReportMissing) onReportMissing({ origin: 'missing_word', search_query: rawQuery }); else setReportUnavailable(true); }}>Повідомити про відсутній запис</Button>{reportUnavailable && <p className="caption dictionary-report-note" role="status">Форма повідомлення ще недоступна. Пошуковий запит залишився на екрані.</p>}</Card> : <section aria-labelledby="dictionary-results-title"><h2 id="dictionary-results-title" className="dictionary-results-title" ref={resultsHeading} tabIndex={-1}>Результати</h2>
        <ul className="dictionary-results">{resource.data.items.map(item => <li key={item.entry_id}><button className="dictionary-result" onClick={() => setSelected(item.entry_id)}><span className="dictionary-result-top"><strong lang="pl">{item.lemma_pl}</strong><span aria-hidden="true">↗</span></span><span className="caption">{parts[item.part_of_speech] ?? item.part_of_speech} · {item.short_meaning_uk}</span><span className="dictionary-match">Збіг: {item.matched_form}</span><span className="dictionary-status">{statuses[item.word_status]}</span></button></li>)}</ul>
        {(resource.data.has_prev || resource.data.has_next) && <nav className="dictionary-pagination" aria-label="Сторінки результатів"><Button mode="outline" size="l" disabled={!resource.data.has_prev} onClick={() => setCursor({ before: resource.data.items[0].entry_id })}>← Попередні</Button><Button mode="outline" size="l" disabled={!resource.data.has_next || !resource.data.next_cursor} onClick={() => setCursor({ after: resource.data.next_cursor! })}>Наступні →</Button></nav>}
      </section>)}
    </>}
  </div>;
}
