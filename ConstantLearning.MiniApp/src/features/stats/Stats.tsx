import { useEffect, useRef, useState } from 'react';
import { BackButton } from '../../components/BackButton';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Button, Card, Result, Spinner } from '../../components/ui';
import { startExtraPractice, TodayApiError, type SessionResponse } from '../today/api';
import { getLearnedWordCard, getLearnedWordsSummary, listLearnedWords, StatsApiError, type LearnedWordCard, type StatsCursor, type StatsSort, type StatsTab } from './api';
import './stats.css';

const tabs: { id: StatsTab; label: string }[] = [
  { id: 'all', label: 'Усі' },
  { id: 'review', label: 'Повторити' },
  { id: 'recalled', label: 'Пригадую' },
];
const sorts: { id: StatsSort; label: string }[] = [
  { id: 'recent', label: 'Нещодавні' },
  { id: 'alpha', label: 'За абеткою' },
  { id: 'weakest', label: 'Найслабші' },
];
const statuses = { not_started: 'Ще не вчив', review: 'На повторення', recalled: 'Вже пригадую' };
const translationKinds = { canonical: 'Основний переклад', variant: 'Додатковий переклад', local: 'Місцевий варіант' };

function terminalMessage(error: unknown): string | undefined {
  if (error instanceof StatsApiError || error instanceof TodayApiError) {
    if (error.status === 401) return 'Відкрийте тренер знову в Telegram, щоб продовжити.';
    if (error.status === 404 && error.body?.code === 'learner.not_onboarded') return 'Спершу налаштуйте тренер.';
  }
}

function WordDetail({ entry, onBack }: { entry: string; onBack: () => void }) {
  const heading = useRef<HTMLHeadingElement>(null);
  const card = useQuery({ queryKey: ['stats', 'word', entry], queryFn: ({ signal }) => getLearnedWordCard(entry, signal), retry: false, refetchOnReconnect: false });
  useEffect(() => { heading.current?.focus(); }, [entry]);
  const missing = card.isError && card.error instanceof StatsApiError && card.error.status === 404;
  return <section className="stats-word" aria-labelledby="stats-word-title">
    <BackButton className="stats-back" onBack={onBack}>← До списку</BackButton>
    <h2 id="stats-word-title" className="stats-hero" ref={heading} tabIndex={-1} lang={card.data ? 'pl' : 'uk'}>{card.data ? card.data.lemma_pl : 'Вивчене слово'}</h2>
    {card.isPending && <div className="loading-state" role="status"><span aria-hidden="true"><Spinner size="s" /></span> Завантажуємо картку…</div>}
    {card.isError && <Result tone="error">{missing ? 'Це слово ще не вивчалося. Поверніться до списку.' : terminalMessage(card.error) ?? 'Не вдалося завантажити картку. Спробуйте ще раз.'}</Result>}
    {card.isError && !missing && !(card.error instanceof StatsApiError && card.error.status === 401) && <Button mode="outline" size="l" stretched onClick={() => { void card.refetch(); }}>Спробувати ще раз</Button>}
    {card.data && <WordCardBody card={card.data} />}
  </section>;
}

function WordCardBody({ card }: { card: LearnedWordCard }) {
  return <>
    <div className="stats-hero-meta">
      {card.transcription_uk && <p className="stats-transcription caption">{card.transcription_uk}</p>}
      <div className="stats-word-meta"><span className="dictionary-status">{statuses[card.status]}</span>{card.is_weak && <span className="stats-weak-badge">Потребує уваги</span>}</div>
    </div>
    <dl className="ios-group" aria-label="Переклади">{card.translations_uk.map((translation, index) => <div className="ios-row stats-translation" key={`${translation.kind}-${index}`}><dt>{translationKinds[translation.kind]}</dt><dd className="ios-value">{translation.text_uk}</dd></div>)}</dl>
    <WordStats stat={card} />
    <h3 className="ios-caption">Розбивка помилок</h3>
    <dl className="ios-group" aria-label="Розбивка помилок">
      <div className="ios-row"><dt>Друкарська</dt><dd className="ios-value">{card.mistake_breakdown.typo}</dd></div>
      <div className="ios-row"><dt>Діакритична</dt><dd className="ios-value">{card.mistake_breakdown.diacritic}</dd></div>
      <div className="ios-row"><dt>«Не знаю»</dt><dd className="ios-value">{card.mistake_breakdown.dont_know}</dd></div>
    </dl>
  </>;
}

function WordStats({ stat }: { stat: { shown_count: number; correct_count: number; incorrect_count: number; streak: number; forgotten_count: number } }) {
  return <dl className="ios-group stats-counters" aria-label="Статистика слова">
    <div className="ios-row"><dt>Показ</dt><dd className="ios-value">{stat.shown_count}</dd></div>
    <div className="ios-row"><dt>Правильно</dt><dd className="ios-value">{stat.correct_count}</dd></div>
    <div className="ios-row"><dt>Неправильно</dt><dd className="ios-value">{stat.incorrect_count}</dd></div>
    <div className="ios-row"><dt>Серія</dt><dd className="ios-value">{stat.streak}</dd></div>
    <div className="ios-row"><dt>Забуте</dt><dd className="ios-value">{stat.forgotten_count}</dd></div>
  </dl>;
}

export default function Stats({ onBack, onOpenSession }: { onBack: () => void; onOpenSession?: (session: SessionResponse) => void }) {
  const [tab, setTab] = useState<StatsTab>('all');
  const [sort, setSort] = useState<StatsSort>('recent');
  const [rawQuery, setRawQuery] = useState('');
  const [cursor, setCursor] = useState<StatsCursor>({});
  const [selected, setSelected] = useState<string | null>(null);
  const [message, setMessage] = useState<string>();
  const listHeading = useRef<HTMLHeadingElement>(null);
  const submitting = useRef(false);

  const summary = useQuery({ queryKey: ['stats', 'summary'], queryFn: ({ signal }) => getLearnedWordsSummary(signal), retry: false, refetchOnReconnect: false });
  const q = rawQuery.trim();
  const list = useQuery({ queryKey: ['stats', 'words', tab, sort, q, cursor], queryFn: ({ signal }) => listLearnedWords({ tab, sort, q, cursor }, signal), retry: false, refetchOnReconnect: false });

  const practice = useMutation({
    mutationFn: startExtraPractice, retry: false,
    onSuccess(value) { setMessage(undefined); onOpenSession?.(value); },
    onError(error) {
      if (error instanceof TodayApiError && error.status === 429) setMessage('Занадто швидко — зачекайте кілька секунд і спробуйте ще раз.');
      else if (error instanceof TodayApiError && error.status === 409) setMessage('Зараз немає слів для практики. Повторення з’являться за розкладом.');
      else setMessage(terminalMessage(error) ?? 'Не вдалося почати практику. Спробуйте ще раз.');
    },
    onSettled() { submitting.current = false; },
  });

  function changeTab(next: StatsTab) { if (next === tab) return; setTab(next); setCursor({}); setSelected(null); }
  function changeSort(next: StatsSort) { setSort(next); setCursor({}); setSelected(null); }
  function changeQuery(value: string) { setRawQuery(value); setCursor({}); setSelected(null); }
  function closeWord() { setSelected(null); requestAnimationFrame(() => listHeading.current?.focus()); }
  function trainWeak() {
    if (submitting.current || practice.isPending) return;
    if (!onOpenSession) { setMessage('Заняття зараз недоступне. Спробуйте відкрити тренер пізніше.'); return; }
    submitting.current = true; setMessage(undefined); practice.mutate();
  }

  if (selected) return <div className="stats">
    <WordDetail entry={selected} onBack={closeWord} />
  </div>;

  const currentSort = sorts.find(entry => entry.id === sort)?.label;
  return <div className="stats">
    <BackButton className="stats-navigation" navLabel="Навігація статистики" onBack={onBack}>← На головну</BackButton>
    <h1 className="ios-title" id="stats-title">Мої слова</h1>
    {summary.isPending && <div className="loading-state" role="status"><Spinner size="s" /> Збираємо статистику…</div>}
    {summary.isError && <Card><Result tone="error">{terminalMessage(summary.error) ?? 'Не вдалося завантажити статистику. Спробуйте ще раз.'}</Result>
      {!(summary.error instanceof StatsApiError && [401, 404].includes(summary.error.status)) && <Button size="l" stretched onClick={() => { void summary.refetch(); }}>Спробувати ще раз</Button>}</Card>}
    {summary.data && <>
      <dl className="stats-summary" aria-label="Зведення вивчених слів">
        <div><dd>{summary.data.seen_count}</dd><dt>Побачено</dt></div>
        <div><dd className="ios-accent">{summary.data.recalled_count}</dd><dt>Пригадую</dt></div>
        <div><dd>{summary.data.review_count}</dd><dt>Повторити</dt></div>
      </dl>
      <Button size="l" stretched loading={practice.isPending} disabled={practice.isPending || summary.data.weak_count === 0} onClick={trainWeak}>{practice.isPending ? 'Готуємо практику…' : summary.data.weak_count > 0 ? `Тренувати слабкі · ${summary.data.weak_count}` : 'Тренувати слабкі'}</Button>
      <div className="stats-status" aria-live="polite">{message && <Result tone="error">{message}</Result>}</div>

      <div className="ios-segmented stats-tabs" role="tablist" aria-label="Фільтр за статусом">
        {tabs.map(entry => <button key={entry.id} role="tab" type="button" aria-selected={tab === entry.id} className={`ios-segment ios-plain${tab === entry.id ? ' ios-segment--on' : ''}`} onClick={() => changeTab(entry.id)}>{entry.label}</button>)}
      </div>
      <div className="stats-search"><label className="sr-only" htmlFor="stats-query">Пошук серед вивчених слів</label><input id="stats-query" type="search" autoComplete="off" spellCheck={false} maxLength={200} value={rawQuery} onChange={event => changeQuery(event.target.value)} placeholder="Пошук" /></div>
      <div className="stats-sort"><span>{currentSort}</span><span className="ios-accent" aria-hidden="true">Сортувати ⌄</span>
        <select aria-label="Сортування" value={sort} onChange={event => changeSort(event.target.value as StatsSort)}>{sorts.map(entry => <option key={entry.id} value={entry.id}>{entry.label}</option>)}</select></div>

      <section aria-labelledby="stats-list-title">
        <h2 id="stats-list-title" className="sr-only" ref={listHeading} tabIndex={-1}>Вивчені слова</h2>
        <div role="status" aria-live="polite" className="stats-announcement">
          {list.isPending && <div className="loading-state"><span aria-hidden="true"><Spinner size="s" /></span> Завантажуємо слова…</div>}
        </div>
        {list.isError && <Card><Result tone="error">{terminalMessage(list.error) ?? 'Не вдалося завантажити список. Спробуйте ще раз.'}</Result>{!(list.error instanceof StatsApiError && [401, 404].includes(list.error.status)) && <Button mode="outline" size="l" stretched onClick={() => { void list.refetch(); }}>Спробувати ще раз</Button>}</Card>}
        {list.data && (list.data.items.length === 0
          ? <Card aria-labelledby="stats-empty-title"><h3 id="stats-empty-title">{q ? 'Нічого не знайшли' : 'Поки що порожньо'}</h3><p className="caption">{q ? 'Спробуйте іншу лему або переклад.' : 'Коли ви почнете вивчати слова, вони з’являться тут зі статистикою.'}</p></Card>
          : <>
            <ul className="ios-group stats-results">{list.data.items.map(item => {
              const total = item.correct_count + item.incorrect_count;
              const dot = item.is_weak ? 'weak' : item.status === 'recalled' ? 'recalled' : 'review';
              return <li key={item.entry_id}><button type="button" className="ios-row ios-plain stats-result" onClick={() => setSelected(item.entry_id)}>
                <span className="stats-result-main"><strong className="stats-lemma" lang="pl">{item.lemma_pl}<span className={`stats-dot stats-dot--${dot}`} aria-hidden="true" /></strong>
                  <span className="stats-result-sub">{item.translation_uk}{item.transcription_uk ? ` · ${item.transcription_uk}` : ''}</span>
                  <span className="sr-only">{statuses[item.status]}{item.is_weak ? ', потребує уваги' : ''}</span></span>
                <span className="stats-ratio"><b aria-label={`правильно ${item.correct_count}, неправильно ${item.incorrect_count}`}>{item.correct_count} <i>/</i> <u>{item.incorrect_count}</u></b>
                  <span className="stats-bar" aria-hidden="true"><s style={{ width: total ? `${Math.round(item.correct_count / total * 100)}%` : '0%' }} /></span></span>
                <span className="ios-value ios-chevron" aria-hidden="true">›</span>
              </button></li>;
            })}</ul>
            <p className="ios-footnote">Праворуч: правильні відповіді й помилки. Смужка показує частку правильних.</p>
            {(list.data.has_prev || list.data.has_next) && <nav className="stats-pagination" aria-label="Сторінки списку"><button type="button" className="ios-plain" disabled={!list.data.has_prev} onClick={() => setCursor({ before: list.data.items[0].entry_id })}>‹ Попередні</button><button type="button" className="ios-plain" disabled={!list.data.has_next || !list.data.next_cursor} onClick={() => setCursor({ after: list.data.next_cursor! })}>Наступні ›</button></nav>}
          </>)}
      </section>
    </>}
  </div>;
}
