import { useCallback, useEffect, useRef, useState } from 'react';
import { BackButton } from '../../components/BackButton';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Result, Spinner } from '../../components/ui';
import { decideReport, EditorApiError, editorQueryKey, listApplied, listQueue, rollbackChange, type AppliedChange, type Cursor, type QueueItem } from './api';
import { accessFailure, decisionBody, decisionFailure, editorDate, fieldLabels, originLabels, previousValue, queueField, type DecisionDraft } from './model';
import { DecisionDialog } from './DecisionDialog';
import { EditorDialog } from './EditorDialog';
import './editor.css';

type View = 'queue' | 'applied';
export default function Editor({ onBack }: { onBack: () => void }) {
  const client = useQueryClient();
  const [view, setView] = useState<View>('queue');
  const [cursors, setCursors] = useState<Record<View, Cursor>>({ queue: {}, applied: {} });
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
  const queryOptions = { retry: false, staleTime: 0, refetchOnWindowFocus: false, refetchOnReconnect: false } as const;
  const queue = useQuery({ ...queryOptions, queryKey: [...editorQueryKey, 'queue', cursors.queue], queryFn: ({ signal }) => listQueue(cursors.queue, signal), enabled: view === 'queue' && !blocked });
  const applied = useQuery({ ...queryOptions, queryKey: [...editorQueryKey, 'applied', cursors.applied], queryFn: ({ signal }) => listApplied(cursors.applied, signal), enabled: view === 'applied' && !blocked });
  const current = view === 'queue' ? queue : applied;
  const readFailure = accessFailure(queue.error) ?? accessFailure(applied.error);
  const access = blocked ?? readFailure;

  const deny = useCallback((reason: 'auth' | 'role') => {
    setBlocked(reason); setDecisionItem(null); setRollbackItem(null); setDrafts({}); setDecisionError(undefined); setRollbackError(undefined); setNotice(undefined);
    void client.cancelQueries({ queryKey: editorQueryKey }).then(() => client.removeQueries({ queryKey: editorQueryKey }));
  }, [client]);
  useEffect(() => { if (readFailure && !blocked) deny(readFailure); }, [readFailure, blocked, deny]);
  useEffect(() => { if (current.isSuccess) setAuthorized(true); }, [current.isSuccess]);

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
  const header = <><BackButton className="editor-navigation" navLabel="Навігація редактора" onBack={onBack} disabled={busy}>← До навчання</BackButton><div className="intro"><p className="eyebrow">ЯКІСТЬ КОЖНОГО СЛОВА</p><h1>Редактор словника</h1><p>Перевіряйте повідомлення й керуйте опублікованими змінами.</p></div></>;
  if (access) return <>{header}<Card aria-labelledby="editor-access-title"><h2 id="editor-access-title">{access === 'role' ? 'Потрібен доступ редактора' : 'Підтвердьте доступ'}</h2><Result tone="error">{access === 'role' ? 'Цей розділ доступний лише редакторам словника.' : 'Відкрийте тренер знову в Telegram, щоб продовжити.'}</Result></Card></>;

  function selectView(next: View) { if (busy) return; setView(next); setNotice(undefined); }
  function page(cursor: Cursor) { if (busy) return; setCursors(value => ({ ...value, [view]: cursor })); setNotice(undefined); focusList(); }
  const data = current.data;
  return <div className="editor">{header}
    {authorized && <nav className="editor-tabs" aria-label="Розділи редактора"><Button size="l" stretched mode={view === 'queue' ? 'filled' : 'outline'} aria-current={view === 'queue' ? 'page' : undefined} disabled={busy} onClick={() => selectView('queue')}>Черга повідомлень</Button><Button size="l" stretched mode={view === 'applied' ? 'filled' : 'outline'} aria-current={view === 'applied' ? 'page' : undefined} disabled={busy} onClick={() => selectView('applied')}>Застосовані зміни</Button></nav>}
    <h2 ref={titleRef} tabIndex={-1} id="editor-view-title">{view === 'queue' ? 'Черга повідомлень' : 'Застосовані зміни'}</h2>
    <p className="caption">{view === 'queue' ? 'Найдавніші повідомлення — спочатку. Автори позначені псевдонімами.' : 'Нові публікації — спочатку. Відкат доступний лише для поточної версії.'}</p>
    {notice && <Result tone={notice.error ? 'error' : 'success'}>{notice.text}</Result>}
    {current.isPending ? <Card><div className="loading-state" role="status"><Spinner size="s" /> {view === 'queue' ? 'Завантажуємо чергу…' : 'Завантажуємо зміни…'}</div></Card>
      : current.isError ? <Card><Result tone="error">{current.error instanceof EditorApiError && current.error.status === 400 ? 'Не вдалося відкрити цю сторінку. Оновіть список із початку.' : 'Не вдалося завантажити список. Спробуйте ще раз.'}</Result><Button size="l" stretched disabled={current.isFetching} onClick={() => { if (current.error instanceof EditorApiError && current.error.status === 400) void refresh(); else void current.refetch(); }}>{current.isFetching ? 'Завантажуємо…' : 'Спробувати ще раз'}</Button></Card>
      : data && <>
        {!data.items.length && <Card><h3>{view === 'queue' ? 'Усе розглянуто' : 'Застосованих змін ще немає'}</h3><p className="caption">{view === 'queue' ? 'Нові повідомлення з’являться тут після перевірки.' : 'Тут з’являться зміни, підтверджені автоматично або редактором.'}</p><Button mode="outline" size="l" stretched disabled={current.isFetching} onClick={() => { void refresh(); }}>Оновити список</Button></Card>}
        {view === 'queue' ? queue.data?.items.map(item => <Card key={item.id} aria-label={`Повідомлення: ${item.lemma_pl ?? 'відсутнє слово'}`}><p className="eyebrow">{originLabels[item.origin]}</p><h3 className="editor-word">{item.lemma_pl ? <span lang="pl">{item.lemma_pl}</span> : 'Відсутнє слово'}</h3><p className="caption">{queueField(item)} · Автор: <strong>{item.reporter_alias}</strong></p><p className="caption"><time dateTime={item.created_at}>{editorDate(item.created_at)}</time></p>
          <p className="editor-excerpt">{item.origin === 'missing_word' ? item.search_query ?? 'Без пошукового запиту' : item.proposed_value ?? 'Пропозицію не вказано'}</p><Button size="l" stretched mode="outline" disabled={busy} onClick={() => { setDecisionItem(item); setDecisionError(undefined); setStaleDecision(false); }}>Розглянути</Button></Card>)
          : applied.data?.items.map(item => <Card key={item.id} aria-label={`Зміна: ${item.lemma_pl}`}><p className="eyebrow">{fieldLabels[item.field]}</p><h3 className="editor-word" lang="pl">{item.lemma_pl}</h3><ChangeValues item={item} /><p className="caption">Підтверджено: {item.confirmed_by === 'editor' ? 'редактором' : 'автоматичною звіркою'}<br /><time dateTime={item.applied_at}>{editorDate(item.applied_at)}</time></p><Button size="l" stretched mode="outline" disabled={!item.rollback_allowed || busy} onClick={() => { if (!item.rollback_allowed) return; setRollbackItem(item); setRollbackError(undefined); }}>{item.rollback_allowed ? 'Відкотити зміну' : 'Відкат недоступний'}</Button>{!item.rollback_allowed && <p className="caption editor-pending">Відкотити можна лише поточну версію до попередньої перевіреної.</p>}</Card>)}
        {!!data.items.length && <nav className="editor-pagination" aria-label="Сторінки списку"><Button size="l" mode="outline" disabled={!data.has_prev || busy || current.isFetching} onClick={() => page({ before: data.items[0].id })}>← Попередня</Button><Button size="l" mode="outline" disabled={!data.has_next || !data.next_cursor || busy || current.isFetching} onClick={() => { if (data.next_cursor) page({ after: data.next_cursor }); }}>Наступна →</Button></nav>}
      </>}
    {decisionItem && <DecisionDialog key={decisionItem.id} item={decisionItem} draft={drafts[decisionItem.id]} pending={decision.isPending} error={decisionError} stale={staleDecision} onDraft={draft => { setDrafts(value => ({ ...value, [decisionItem.id]: draft })); setDecisionError(undefined); }} onClose={() => { if (!decision.isPending) setDecisionItem(null); }} onSubmit={draft => { if (deciding.current || decision.isPending || staleDecision) return; deciding.current = true; setDecisionError(undefined); decision.mutate({ item: decisionItem, draft }); }} />}
    {rollbackItem && <EditorDialog title="Відкотити зміну?" pending={rollback.isPending} onClose={() => { if (!rollback.isPending) setRollbackItem(null); }}><p className="editor-context"><span lang="pl">{rollbackItem.lemma_pl}</span> · {fieldLabels[rollbackItem.field]}</p><ChangeValues item={rollbackItem} /><p className="caption">Попередня версія знову стане доступною всім. Історія відповідей, статуси слів і результат закритого повідомлення залишаться без змін.</p>{rollbackError && <Result tone="error">{rollbackError}</Result>}<div className="editor-actions"><Button size="l" stretched disabled={rollback.isPending} onClick={() => { if (rollingBack.current || rollback.isPending || !rollbackItem.rollback_allowed) return; rollingBack.current = true; setRollbackError(undefined); rollback.mutate(rollbackItem); }}>{rollback.isPending ? 'Відновлюємо версію…' : 'Підтвердити відкат'}</Button><Button size="l" mode="outline" stretched disabled={rollback.isPending} onClick={() => setRollbackItem(null)}>Залишити без змін</Button></div>{rollback.isPending && <p className="caption editor-pending" role="status">Відновлюємо версію…</p>}</EditorDialog>}
  </div>;
}

function ChangeValues({ item }: { item: AppliedChange }) { return <dl className="editor-values"><div><dt>Попереднє значення</dt><dd>{previousValue(item)}</dd></div><div><dt>Нове значення</dt><dd>{item.new_value}</dd></div></dl>; }
