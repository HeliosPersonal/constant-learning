import { useCallback, useEffect, useRef, useState } from 'react';
import { BackButton } from '../../components/BackButton';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Result, Spinner } from '../../components/ui';
import { decideReport, EditorApiError, editorQueryKey, listApplied, listQueue, rollbackChange, type AppliedChange, type Cursor, type QueueItem } from './api';
import { languageCatalogQueryKey, listTargetLanguages } from '../settings/language/api';
import { accessFailure, decisionBody, decisionFailure, editorDate, fieldLabels, originLabels, previousValue, queueField, type DecisionDraft } from './model';
import { DecisionScreen } from './DecisionScreen';
import './editor.css';

type View = 'queue' | 'applied';
export default function Editor({ onBack }: { onBack: () => void }) {
  const client = useQueryClient();
  const [view, setView] = useState<View>('queue');
  const [cursors, setCursors] = useState<Record<View, Cursor>>({ queue: {}, applied: {} });
  const [language, setLanguage] = useState('');
  const [authorized, setAuthorized] = useState(false);
  const [blocked, setBlocked] = useState<'auth' | 'role' | null>(null);
  const [decisionItem, setDecisionItem] = useState<QueueItem | null>(null);
  const [rollbackItem, setRollbackItem] = useState<AppliedChange | null>(null);
  const [drafts, setDrafts] = useState<Record<string, DecisionDraft>>({});
  const [decisionError, setDecisionError] = useState<string>();
  const [staleDecision, setStaleDecision] = useState(false);
  const [rollbackError, setRollbackError] = useState<string>();
  const [notice, setNotice] = useState<{ text: string; error?: boolean }>();
  const deciding = useRef(false);
  const rollingBack = useRef(false);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const triggerRef = useRef<HTMLElement | null>(null);
  const wasSubscreen = useRef(false);
  const queryOptions = { retry: false, staleTime: 0, refetchOnWindowFocus: false, refetchOnReconnect: false } as const;
  const queue = useQuery({ ...queryOptions, queryKey: [...editorQueryKey, 'queue', language, cursors.queue], queryFn: ({ signal }) => listQueue(cursors.queue, language || undefined, signal), enabled: view === 'queue' && !blocked });
  const applied = useQuery({ ...queryOptions, queryKey: [...editorQueryKey, 'applied', cursors.applied], queryFn: ({ signal }) => listApplied(cursors.applied, signal), enabled: view === 'applied' && !blocked });
  const languages = useQuery({ ...queryOptions, queryKey: languageCatalogQueryKey, queryFn: ({ signal }) => listTargetLanguages(signal), enabled: !blocked && authorized });
  const current = view === 'queue' ? queue : applied;
  const readFailure = accessFailure(queue.error) ?? accessFailure(applied.error);
  const access = blocked ?? readFailure;

  const deny = useCallback((reason: 'auth' | 'role') => {
    setBlocked(reason); setDecisionItem(null); setRollbackItem(null); setDrafts({}); setDecisionError(undefined); setRollbackError(undefined); setNotice(undefined);
    void client.cancelQueries({ queryKey: editorQueryKey }).then(() => client.removeQueries({ queryKey: editorQueryKey }));
  }, [client]);
  useEffect(() => { if (readFailure && !blocked) deny(readFailure); }, [readFailure, blocked, deny]);
  useEffect(() => { if (current.isSuccess) setAuthorized(true); }, [current.isSuccess]);

  const subscreenOpen = !!decisionItem || !!rollbackItem;
  useEffect(() => {
    if (wasSubscreen.current && !subscreenOpen) {
      const trigger = triggerRef.current;
      if (trigger?.isConnected && !trigger.matches(':disabled')) trigger.focus(); else titleRef.current?.focus();
    }
    wasSubscreen.current = subscreenOpen;
  }, [subscreenOpen]);

  const refresh = async () => {
    setCursors({ queue: {}, applied: {} });
    await client.invalidateQueries({ queryKey: editorQueryKey });
  };
  const focusList = () => requestAnimationFrame(() => titleRef.current?.focus());
  const decision = useMutation({
    mutationFn: ({ item, draft }: { item: QueueItem; draft: DecisionDraft }) => decideReport(item.id, decisionBody(draft)), retry: false,
    async onSuccess(result) {
      setDecisionItem(null); setDecisionError(undefined);
      setNotice({ text: result.outcome === 'added_to_candidates' ? 'Лему додано до кандидатів. Для навчання вона стане доступною після перевірок.' : result.outcome === 'rejected' ? 'Повідомлення відхилено з обраною причиною.' : 'Зміну застосовано. Вона доступна в списку застосованих змін.' });
      await refresh(); focusList();
    },
    async onError(error) {
      const failure = accessFailure(error); if (failure) { deny(failure); return; }
      setDecisionError(decisionFailure(error));
      if (error instanceof EditorApiError && (error.status === 404 || error.status === 409)) { setStaleDecision(true); await refresh(); }
    },
    onSettled() { deciding.current = false; },
  });
  const rollback = useMutation({
    mutationFn: (item: AppliedChange) => rollbackChange(item.id), retry: false,
    async onSuccess() {
      setRollbackItem(null); setRollbackError(undefined); setNotice({ text: 'Попередню версію відновлено. Навчальна історія та закрите повідомлення залишилися без змін.' });
      await refresh(); focusList();
    },
    async onError(error) {
      const failure = accessFailure(error); if (failure) { deny(failure); return; }
      if (error instanceof EditorApiError && (error.status === 409 || error.status === 404)) {
        setRollbackItem(null); setNotice({ error: true, text: error.status === 409 ? 'Відкотити можна лише поточну версію запису. Список оновлено — перевірте доступні дії.' : 'Зміну більше не знайдено. Список оновлено.' });
        await refresh(); focusList();
      } else setRollbackError('Не вдалося відкотити зміну. Нічого не підтверджено — спробуйте ще раз.');
    },
    onSettled() { rollingBack.current = false; },
  });
  const busy = decision.isPending || rollback.isPending;
  const back = <BackButton className="editor-navigation" navLabel="Навігація редактора" onBack={onBack} disabled={busy}>← До навчання</BackButton>;
  if (access) return <div className="editor">{back}<h1 className="ios-title">Редактор словника</h1><section className="editor-empty" aria-labelledby="editor-access-title"><h2 id="editor-access-title">{access === 'role' ? 'Потрібен доступ редактора' : 'Підтвердьте доступ'}</h2><Result tone="error">{access === 'role' ? 'Цей розділ доступний лише редакторам словника.' : 'Відкрийте тренер знову в Telegram, щоб продовжити.'}</Result></section></div>;

  function selectView(next: View) { if (busy) return; setView(next); setNotice(undefined); }
  function selectLanguage(next: string) { if (busy) return; setLanguage(next); setCursors(value => ({ ...value, queue: {} })); setNotice(undefined); focusList(); }
  function page(cursor: Cursor) { if (busy) return; setCursors(value => ({ ...value, [view]: cursor })); setNotice(undefined); focusList(); }
  function openDecision(item: QueueItem, trigger: HTMLElement) { triggerRef.current = trigger; setDecisionItem(item); setDecisionError(undefined); setStaleDecision(false); }
  function openRollback(item: AppliedChange, trigger: HTMLElement) { if (!item.rollback_allowed) return; triggerRef.current = trigger; setRollbackItem(item); setRollbackError(undefined); }
  const data = current.data;
  const subscreen = !!decisionItem || !!rollbackItem;
  return <div className="editor">
    <div hidden={subscreen}>
      {back}
      <h1 className="ios-title" id="editor-view-title" ref={titleRef} tabIndex={-1}>Редактор словника</h1>
      <div className="ios-status" role="status" aria-live="polite">{notice && <span className={notice.error ? 'ios-danger' : undefined}>{notice.text}</span>}</div>
      {authorized && <div className="ios-segmented editor-tabs" role="tablist" aria-label="Розділи редактора">
        {([['queue', 'Черга'], ['applied', 'Застосовані']] as const).map(([id, label]) => <button key={id} type="button" role="tab" aria-selected={view === id} className={`ios-segment ios-plain${view === id ? ' ios-segment--on' : ''}`} disabled={busy} onClick={() => selectView(id)}>{label}</button>)}
      </div>}
      {authorized && view === 'queue' && <div className="editor-language-filter">
        <label htmlFor="editor-language-filter">Мова черги</label>
        <select id="editor-language-filter" value={language} disabled={busy} onChange={event => selectLanguage(event.target.value)}>
          <option value="">Усі мови</option>
          {languages.data?.map(item => <option key={item.code} value={item.code}>{item.display_name_uk}</option>)}
        </select>
      </div>}
      {current.isPending ? <div className="loading-state" role="status"><Spinner size="s" /> {view === 'queue' ? 'Завантажуємо чергу…' : 'Завантажуємо зміни…'}</div>
        : current.isError ? <div className="editor-empty"><Result tone="error">{current.error instanceof EditorApiError && current.error.status === 400 ? 'Не вдалося відкрити цю сторінку. Оновіть список із початку.' : 'Не вдалося завантажити список. Спробуйте ще раз.'}</Result><Button size="l" stretched disabled={current.isFetching} onClick={() => { if (current.error instanceof EditorApiError && current.error.status === 400) void refresh(); else void current.refetch(); }}>{current.isFetching ? 'Завантажуємо…' : 'Спробувати ще раз'}</Button></div>
        : data && <>
          {!data.items.length && <div className="editor-empty"><h3>{view === 'queue' ? 'Усе розглянуто' : 'Застосованих змін ще немає'}</h3><p className="caption">{view === 'queue' ? 'Нові повідомлення з’являться тут після перевірки.' : 'Тут з’являться зміни, підтверджені автоматично або редактором.'}</p><Button mode="outline" size="l" stretched disabled={current.isFetching} onClick={() => { void refresh(); }}>Оновити список</Button></div>}
          {!!data.items.length && <div className="ios-caption">{view === 'queue' ? 'Найдавніші — спочатку' : 'Нові публікації — спочатку'}</div>}
          {!!data.items.length && (view === 'queue'
            ? <ul className="ios-group editor-list">{queue.data?.items.map(item => <li key={item.id}><section aria-label={`Повідомлення: ${item.lemma ?? 'відсутнє слово'}`}><button type="button" className="ios-row ios-plain editor-item" disabled={busy} onClick={event => openDecision(item, event.currentTarget)}>
              <span className="editor-item-main"><span className="editor-item-word">{item.lemma ? <span lang={item.target_language}>{item.lemma}</span> : 'Відсутнє слово'}</span>
                <span className="editor-item-sub">{originLabels[item.origin]} · {queueField(item)}</span>
                <span className="editor-item-excerpt">{item.origin === 'missing_word' ? item.search_query ?? 'Без пошукового запиту' : item.proposed_value ?? 'Пропозицію не вказано'}</span>
                <span className="editor-item-sub">Автор: <strong>{item.reporter_alias}</strong> · <time dateTime={item.created_at}>{editorDate(item.created_at)}</time></span><span className="sr-only">Розглянути</span></span>
              <span className="ios-value ios-chevron" aria-hidden="true">›</span></button></section></li>)}</ul>
            : <ul className="ios-group editor-list">{applied.data?.items.map(item => <li key={item.id}><section className="editor-applied" aria-label={`Зміна: ${item.lemma_pl}`}>
              <div className="editor-applied-head"><h3 className="editor-item-word" lang="pl">{item.lemma_pl}</h3><button type="button" className={`editor-rollback ios-plain${item.rollback_allowed ? '' : ' editor-rollback--off'}`} disabled={!item.rollback_allowed || busy} onClick={event => openRollback(item, event.currentTarget)}>{item.rollback_allowed ? 'Відкотити' : 'Лише поточна'}</button></div>
              <p className="editor-item-sub">{fieldLabels[item.field]}</p>
              <p className="editor-change"><s>{previousValue(item)}</s> → <span>{item.new_value}</span></p>
              <p className="editor-item-sub">Підтверджено: {item.confirmed_by === 'editor' ? 'редактором' : 'автоматичною звіркою'}<br /><time dateTime={item.applied_at}>{editorDate(item.applied_at)}</time></p>
            </section></li>)}</ul>)}
          {view === 'applied' && !!data.items.length && <p className="ios-footnote">Відкотити можна лише поточну версію запису до попередньої перевіреної.</p>}
          {!!data.items.length && <nav className="editor-pagination" aria-label="Сторінки списку"><button type="button" className="ios-plain" disabled={!data.has_prev || busy || current.isFetching} onClick={() => page({ before: data.items[0].id })}>‹ Попередня</button><button type="button" className="ios-plain" disabled={!data.has_next || !data.next_cursor || busy || current.isFetching} onClick={() => { if (data.next_cursor) page({ after: data.next_cursor }); }}>Наступна ›</button></nav>}
        </>}
    </div>
    {decisionItem && <DecisionScreen key={decisionItem.id} item={decisionItem} draft={drafts[decisionItem.id]} pending={decision.isPending} error={decisionError} stale={staleDecision} onDraft={draft => { setDrafts(value => ({ ...value, [decisionItem.id]: draft })); setDecisionError(undefined); }} onClose={() => { if (!decision.isPending) setDecisionItem(null); }} onSubmit={draft => { if (deciding.current || decision.isPending || staleDecision) return; deciding.current = true; setDecisionError(undefined); decision.mutate({ item: decisionItem, draft }); }} />}
    {rollbackItem && <RollbackScreen item={rollbackItem} pending={rollback.isPending} error={rollbackError} onClose={() => { if (!rollback.isPending) setRollbackItem(null); }} onConfirm={() => { if (rollingBack.current || rollback.isPending || !rollbackItem.rollback_allowed) return; rollingBack.current = true; setRollbackError(undefined); rollback.mutate(rollbackItem); }} />}
  </div>;
}

/** Екран підтвердження відкату: що зараз, що буде після відкату, одна небезпечна дія. */
function RollbackScreen({ item, pending, error, onClose, onConfirm }: { item: AppliedChange; pending: boolean; error?: string; onClose: () => void; onConfirm: () => void }) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => { headingRef.current?.focus(); window.scrollTo?.(0, 0); }, []);
  return <section className="editor-screen" aria-labelledby="editor-screen-title">
    <BackButton className="editor-navigation" navLabel="Навігація відкату" disabled={pending} onBack={onClose}>← До змін</BackButton>
    <h1 className="ios-title" id="editor-screen-title" ref={headingRef} tabIndex={-1}>Відкотити зміну?</h1>
    <p className="ios-caption editor-context"><span lang="pl">{item.lemma_pl}</span> · {fieldLabels[item.field]}</p>
    <dl className="ios-group editor-values">
      <div className="ios-row"><dt>Зараз</dt><dd className="ios-value">{item.new_value}</dd></div>
      <div className="ios-row"><dt>Після відкату</dt><dd className="ios-value">{previousValue(item)}</dd></div>
    </dl>
    <p className="ios-footnote">Попередня версія знову стане доступною всім. Історія відповідей, статуси слів і результат закритого повідомлення залишаться без змін.</p>
    {error && <p className="ios-footnote ios-footnote--error" role="alert">{error}</p>}
    <button type="button" className="editor-primary editor-primary--danger ios-plain" disabled={pending} onClick={onConfirm}>{pending ? 'Відновлюємо версію…' : 'Підтвердити відкат'}</button>
    <button type="button" className="editor-secondary ios-plain" disabled={pending} onClick={onClose}>Залишити без змін</button>
    {pending && <p className="ios-footnote" role="status">Відновлюємо версію…</p>}
  </section>;
}
