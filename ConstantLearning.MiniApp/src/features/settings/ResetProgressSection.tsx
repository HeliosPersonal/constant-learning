import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Dialog, Result } from '../../components/ui';
import { learnerQueryKey } from '../onboarding/api';
import { todayQueryKey } from '../today/api';
import { ProgressResetApiError, resetProgress } from './reset';

/**
 * Небезпечна дія внизу «Налаштування»: кнопка скидання відкриває діалог підтвердження з переліком
 * стертого й збереженого та попередженням про незворотність (AC-02). «Скасувати» або закриття нічого
 * не стирає (AC-03). Після успіху кеш learner і «Сьогодні» скидається, а learner повертається на
 * «Сьогодні» з повідомленням про результат (AC-01, AC-09).
 */
export function ResetProgressSection({ onDone }: { onDone: (notice: string) => void }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string>();
  const mutation = useMutation({
    mutationFn: resetProgress, retry: false,
    async onSuccess(erased) {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: learnerQueryKey }),
        queryClient.invalidateQueries({ queryKey: todayQueryKey }),
      ]);
      queryClient.removeQueries({ queryKey: ['session', 'current'] });
      onDone(erased ? 'Прогрес скинуто. Починаємо з чистого аркуша.' : 'Прогрес уже був порожнім — нічого не змінено.');
    },
    onError(cause) {
      if (cause instanceof ProgressResetApiError && cause.status === 401) setError('Відкрийте тренер знову в Telegram, щоб продовжити.');
      else if (cause instanceof ProgressResetApiError && cause.status === 404) setError('Спершу налаштуйте тренер.');
      else setError('Не вдалося скинути прогрес. Нічого не змінено — спробуйте ще раз.');
    },
  });

  function close() { if (!mutation.isPending) { setOpen(false); setError(undefined); } }

  return <section className="settings-danger" aria-labelledby="settings-reset-title">
    <h2 id="settings-reset-title" className="sr-only">Скидання прогресу</h2>
    <div className="ios-group"><button type="button" className="ios-row ios-plain ios-danger" onClick={() => { setError(undefined); setOpen(true); }}>Скинути прогрес…</button></div>
    <p className="ios-footnote">Стирає стан слів, заняття й відповіді. Профіль і налаштування лишаються.</p>
    {error && !open && <p className="ios-footnote ios-footnote--error" role="alert">{error}</p>}
    <Dialog open={open} onClose={close} title="Стерти весь прогрес?" actions={
      <div className="settings-reset-actions">
        <Button size="l" mode="plain" stretched disabled={mutation.isPending} onClick={close}>Скасувати</Button>
        <Button size="l" stretched loading={mutation.isPending} disabled={mutation.isPending} onClick={() => { setError(undefined); mutation.mutate(); }}>{mutation.isPending ? 'Стираємо прогрес…' : 'Стерти прогрес'}</Button>
      </div>
    }>
      <p>Буде стерто без можливості відновлення:</p>
      <ul className="settings-reset-list">
        <li>стан усіх вивчених слів і повторень;</li>
        <li>усі заняття, зокрема розпочате;</li>
        <li>усі відповіді й оскарження відповідей.</li>
      </ul>
      <p>Лишиться без змін:</p>
      <ul className="settings-reset-list">
        <li>денний ліміт нових слів і відкладена зміна;</li>
        <li>часовий пояс і нагадування;</li>
        <li>надіслані скарги на контент.</li>
      </ul>
      <p className="caption settings-reset-warning">Цю дію не можна скасувати.</p>
      {error && <Result tone="error">{error}</Result>}
    </Dialog>
  </section>;
}
