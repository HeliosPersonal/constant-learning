import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Button, Card, Result, Spinner } from '../../components/ui';
import { startExtraPractice, TodayApiError, type SessionResponse } from '../today/api';
import { getLearnedWordCard, getLearnedWordsSummary, listLearnedWords, StatsApiError, type LearnedWordCard, type StatsCursor, type StatsSort, type StatsTab } from './api';
import './stats.css';

const tabs: { id: StatsTab; label: string }[] = [
  { id: 'all', label: 'Усі' },
  { id: 'review', label: 'На повторення' },
  { id: 'recalled', label: 'Вже пригадую' },
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
  return <div className="stats-word">
    <Button mode="plain" size="l" onClick={onBack}>← До списку</Button>
    <Card aria-labelledby="stats-word-title">
      <p className="eyebrow">КАРТКА СЛОВА</p>
      <h2 id="stats-word-title" ref={heading} tabIndex={-1} lang={card.data ? 'pl' : 'uk'}>{card.data ? card.data.lemma_pl : 'Вивчене слово'}</h2>
      {card.isPending && <div className="loading-state" role="status"><span aria-hidden="true"><Spinner size="s" /></span> Завантажуємо картку…</div>}
      {card.isError && <Result tone="error">{missing ? 'Це слово ще не вивчалося. Поверніться до списку.' : terminalMessage(card.error) ?? 'Не вдалося завантажити картку. Спробуйте ще раз.'}</Result>}
      {card.isError && !missing && !(card.error instanceof StatsApiError && card.error.status === 401) && <Button mode="outline" size="l" stretched onClick={() => { void card.refetch(); }}>Спробувати ще раз</Button>}
      {card.data && <WordCardBody card={card.data} />}
    </Card>
  </div>;
}

function WordCardBody({ card }: { card: LearnedWordCard }) {
  return <>
    {card.transcription_uk && <p className="stats-transcription caption">{card.transcription_uk}</p>}
    <div className="stats-word-meta"><span className="dictionary-status">{statuses[card.status]}</span>{card.is_weak && <span className="stats-weak-badge">Потребує уваги</span>}</div>
    <dl className="stats-translations" aria-label="Переклади">{card.translations_uk.map((translation, index) => <div key={`${translation.kind}-${index}`}><dt>{translationKinds[translation.kind]}</dt><dd>{translation.text_uk}</dd></div>)}</dl>
    <WordStats stat={card} />
    <div className="stats-mistakes"><h3>Розбивка помилок</h3>
      <dl className="stats-mistake-grid" aria-label="Розбивка помилок">
        <div><dt>Друкарська</dt><dd>{card.mistake_breakdown.typo}</dd></div>
        <div><dt>Діакритична</dt><dd>{card.mistake_breakdown.diacritic}</dd></div>
        <div><dt>«Не знаю»</dt><dd>{card.mistake_breakdown.dont_know}</dd></div>
      </dl>
    </div>
  </>;
}

function WordStats({ stat }: { stat: { shown_count: number; correct_count: number; incorrect_count: number; streak: number; forgotten_count: number } }) {
  return <dl className="stats-counters" aria-label="Статистика слова">
    <div><dt>Показ</dt><dd>{stat.shown_count}</dd></div>
    <div><dt>Правильно</dt><dd>{stat.correct_count}</dd></div>
    <div><dt>Неправильно</dt><dd>{stat.incorrect_count}</dd></div>
    <div><dt>Серія</dt><dd>{stat.streak}</dd></div>
    <div><dt>Забуте</dt><dd>{stat.forgotten_count}</dd></div>
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

  const heading = <><p className="eyebrow">ВАШІ ЗДОБУТКИ</p><h1 id="stats-title">Мої слова</h1></>;

  if (selected) return <div className="stats">
    <nav className="stats-navigation" aria-label="Навігація статистики"><Button mode="plain" size="l" onClick={onBack}>← На головну</Button></nav>
    <WordDetail entry={selected} onBack={closeWord} />
  </div>;

  return <div className="stats">
    <nav className="stats-navigation" aria-label="Навігація статистики"><Button mode="plain" size="l" onClick={onBack}>← На головну</Button></nav>
    <Card aria-labelledby="stats-title">{heading}
      {summary.isPending && <div className="loading-state" role="status"><Spinner size="s" /> Збираємо статистику…</div>}
      {summary.isError && <Result tone="error">{terminalMessage(summary.error) ?? 'Не вдалося завантажити статистику. Спробуйте ще раз.'}</Result>}
      {summary.isError && !(summary.error instanceof StatsApiError && summary.error.status === 401) && !(summary.error instanceof StatsApiError && summary.error.status === 404) && <Button size="l" stretched onClick={() => { void summary.refetch(); }}>Спробувати ще раз</Button>}
      {summary.data && <>
        <dl className="stats-summary" aria-label="Зведення вивчених слів">
          <div><dt>Побачено</dt><dd>{summary.data.seen_count}</dd></div>
          <div><dt>Вже пригадую</dt><dd>{summary.data.recalled_count}</dd></div>
          <div><dt>На повторення</dt><dd>{summary.data.review_count}</dd></div>
          <div><dt>Потребують уваги</dt><dd>{summary.data.weak_count}</dd></div>
        </dl>
        <Button size="l" stretched loading={practice.isPending} disabled={practice.isPending || summary.data.weak_count === 0} onClick={trainWeak}>{practice.isPending ? 'Готуємо практику…' : 'Тренувати слабкі'}</Button>
        <div className="stats-status" aria-live="polite">{message && <Result tone="error">{message}</Result>}</div>
      </>}
    </Card>

    {summary.data && <>
      <div className="stats-tabs" role="tablist" aria-label="Фільтр за статусом">
        {tabs.map(entry => <button key={entry.id} role="tab" type="button" aria-selected={tab === entry.id} className={`stats-tab${tab === entry.id ? ' stats-tab--active' : ''}`} onClick={() => changeTab(entry.id)}>{entry.label}</button>)}
      </div>
      <div className="stats-controls">
        <div className="stats-search"><label htmlFor="stats-query">Пошук серед вивчених слів</label><input id="stats-query" type="search" autoComplete="off" spellCheck={false} maxLength={200} value={rawQuery} onChange={event => changeQuery(event.target.value)} placeholder="Лема або переклад" /></div>
        <div className="stats-sort"><label htmlFor="stats-sort">Сортування</label><select id="stats-sort" value={sort} onChange={event => changeSort(event.target.value as StatsSort)}>{sorts.map(entry => <option key={entry.id} value={entry.id}>{entry.label}</option>)}</select></div>
      </div>

      <section aria-labelledby="stats-list-title">
        <h2 id="stats-list-title" className="stats-list-title" ref={listHeading} tabIndex={-1}>Вивчені слова</h2>
        <div role="status" aria-live="polite" className="stats-announcement">
          {list.isPending && <div className="loading-state"><span aria-hidden="true"><Spinner size="s" /></span> Завантажуємо слова…</div>}
        </div>
        {list.isError && <Card><Result tone="error">{terminalMessage(list.error) ?? 'Не вдалося завантажити список. Спробуйте ще раз.'}</Result>{!(list.error instanceof StatsApiError && [401, 404].includes(list.error.status)) && <Button mode="outline" size="l" stretched onClick={() => { void list.refetch(); }}>Спробувати ще раз</Button>}</Card>}
        {list.data && (list.data.items.length === 0
          ? <Card aria-labelledby="stats-empty-title"><h3 id="stats-empty-title">{q ? 'Нічого не знайшли' : 'Поки що порожньо'}</h3><p className="caption">{q ? 'Спробуйте іншу лему або переклад.' : 'Коли ви почнете вивчати слова, вони з’являться тут зі статистикою.'}</p></Card>
          : <>
            <ul className="stats-results">{list.data.items.map(item => <li key={item.entry_id}><button type="button" className={`stats-result${item.is_weak ? ' stats-result--weak' : ''}`} onClick={() => setSelected(item.entry_id)}>
              <span className="stats-result-top"><strong lang="pl">{item.lemma_pl}</strong><span className="dictionary-status">{statuses[item.status]}</span></span>
              <span className="caption stats-result-translation">{item.translation_uk}</span>
              {item.transcription_uk && <span className="caption stats-result-transcription">{item.transcription_uk}</span>}
              <span className="stats-result-counters caption"><span>показ {item.shown_count}</span><span>правильно {item.correct_count}</span><span>неправильно {item.incorrect_count}</span><span>серія {item.streak}</span><span>забуте {item.forgotten_count}</span></span>
            </button></li>)}</ul>
            {(list.data.has_prev || list.data.has_next) && <nav className="stats-pagination" aria-label="Сторінки списку"><Button mode="outline" size="l" disabled={!list.data.has_prev} onClick={() => setCursor({ before: list.data.items[0].entry_id })}>← Попередні</Button><Button mode="outline" size="l" disabled={!list.data.has_next || !list.data.next_cursor} onClick={() => setCursor({ after: list.data.next_cursor! })}>Наступні →</Button></nav>}
          </>)}
      </section>
    </>}
  </div>;
}
