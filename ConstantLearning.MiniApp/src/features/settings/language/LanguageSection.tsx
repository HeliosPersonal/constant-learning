import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Spinner } from '../../../components/ui';
import { learnerQueryKey } from '../../onboarding/api';
import { todayQueryKey } from '../../today/api';
import {
  LanguageApiError,
  languageCatalogQueryKey,
  learnerLanguagesQueryKey,
  listLearnerLanguages,
  listTargetLanguages,
  reportLanguageSwitch,
  setActiveLanguage,
  startLearnerLanguage,
} from './api';

function errorMessage(cause: unknown): string {
  if (cause instanceof LanguageApiError && cause.status === 401) return 'Відкрийте тренер знову в Telegram, щоб продовжити.';
  if (cause instanceof LanguageApiError && cause.status === 404) return 'Спершу налаштуйте тренер.';
  if (cause instanceof LanguageApiError && cause.body?.code === 'language.not_available') return 'Ця мова поки недоступна.';
  if (cause instanceof LanguageApiError && cause.body?.code === 'language.concurrent_update') return 'Стан змінився — повторіть спробу.';
  return 'Не вдалося змінити мову навчання. Нічого не змінено — спробуйте ще раз.';
}

/**
 * Вибір і перемикання мови навчання в налаштуваннях (OQ-2): learner обирає активну мову серед уже
 * вивчених і додає нові з каталогу. Недоступні мови не можна почати й пояснюється чому (AC-02). Після
 * зміни кеш «Сьогодні», learner і список мов скидається, тож екрани показують нову мову (AC-03, AC-07).
 * Тривалість перемикання йде в телеметрію (NFR ≤1500 ms).
 */
export function LanguageSection() {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string>();
  const startedAt = useRef(0);
  const catalog = useQuery({ queryKey: languageCatalogQueryKey, queryFn: ({ signal }) => listTargetLanguages(signal), retry: false, refetchOnWindowFocus: false });
  const learner = useQuery({ queryKey: learnerLanguagesQueryKey, queryFn: ({ signal }) => listLearnerLanguages(signal), retry: false, refetchOnWindowFocus: false });

  async function applied() {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: todayQueryKey }),
      queryClient.invalidateQueries({ queryKey: learnerQueryKey }),
      queryClient.invalidateQueries({ queryKey: learnerLanguagesQueryKey }),
    ]);
    queryClient.removeQueries({ queryKey: ['session', 'current'] });
  }

  const switchLanguage = useMutation({
    mutationFn: setActiveLanguage, retry: false,
    async onSuccess() { void reportLanguageSwitch(performance.now() - startedAt.current); await applied(); },
    onError(cause) { setError(errorMessage(cause)); },
  });
  const addLanguage = useMutation({
    mutationFn: startLearnerLanguage, retry: false,
    async onSuccess() { void reportLanguageSwitch(performance.now() - startedAt.current); await applied(); },
    onError(cause) { setError(errorMessage(cause)); },
  });

  const busy = switchLanguage.isPending || addLanguage.isPending;
  function pick(code: string, mutate: (code: string) => void) { if (busy) return; setError(undefined); startedAt.current = performance.now(); mutate(code); }

  if (catalog.isLoading || learner.isLoading) {
    return <><div className="ios-caption">Мова навчання</div>
      <div className="ios-group"><div className="ios-row"><span><Spinner size="s" /> Завантажуємо мови…</span></div></div></>;
  }
  if (catalog.isError || learner.isError || !catalog.data || !learner.data) {
    return <><div className="ios-caption">Мова навчання</div>
      <p className="ios-footnote ios-footnote--error" role="alert">Не вдалося завантажити мови. Поверніться пізніше.</p></>;
  }

  const order = new Map(catalog.data.map((language, index) => [language.code, index]));
  const studied = [...learner.data.items].sort((a, b) => (order.get(a.target_language) ?? 0) - (order.get(b.target_language) ?? 0));
  const studiedCodes = new Set(studied.map(language => language.target_language));
  const addable = catalog.data.filter(language => !studiedCodes.has(language.code));

  return <section aria-labelledby="settings-language-title" aria-busy={busy}>
    <h2 id="settings-language-title" className="sr-only">Мова навчання</h2>
    <div className="ios-caption">Мова навчання</div>
    {studied.length === 0
      ? <p className="ios-footnote">Ви ще не почали жодної мови. Оберіть мову нижче, щоб побачити «Сьогодні».</p>
      : <div className="ios-group" role="radiogroup" aria-label="Активна мова навчання">
        {studied.map(language => <button key={language.target_language} type="button" role="radio" aria-checked={language.is_active}
          className="ios-row ios-plain" disabled={busy} onClick={() => !language.is_active && pick(language.target_language, switchLanguage.mutate)}>
          <span>{language.display_name_uk}</span>
          <span className="ios-value" aria-hidden="true">{language.is_active ? '✓' : ''}</span>
        </button>)}
      </div>}
    <p className="ios-footnote">Активна мова визначає «Сьогодні», заняття й словник. Облік решти мов зберігається.</p>

    {addable.length > 0 && <>
      <div className="ios-caption">Додати мову</div>
      <div className="ios-group">
        {addable.map(language => <button key={language.code} type="button" className="ios-row ios-plain"
          disabled={busy || !language.available} onClick={() => pick(language.code, addLanguage.mutate)}>
          <span>{language.display_name_uk}</span>
          <span className="ios-value" aria-hidden="true">{language.available ? '＋' : 'скоро'}</span>
        </button>)}
      </div>
      {addable.some(language => !language.available) && <p className="ios-footnote">Деякі мови поки недоступні — вони зʼявляться, коли буде готовий контент.</p>}
    </>}

    {busy && <p className="ios-footnote" role="status">Змінюємо мову…</p>}
    {error && <p className="ios-footnote ios-footnote--error" role="alert">{error}</p>}
  </section>;
}
