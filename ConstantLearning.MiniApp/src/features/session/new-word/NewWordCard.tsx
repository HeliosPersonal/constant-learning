import { Button } from '../../../components/ui';
import type { components } from '../../../api/schema';

const genders: Record<string, string> = { m: 'чоловічий рід', f: 'жіночий рід', n: 'середній рід', pl: 'множина' };
type Item = components['schemas']['SessionItem'];
export function NewWordCard({ item, pending, onAction }: { item: Item; pending: boolean; onAction: (action: 'acknowledge' | 'already-known') => void }) {
  const card = item.card;
  if (!card) return <p role="alert">Дані картки недоступні. Поверніться на сторінку «Сьогодні».</p>;
  return <><p className="eyebrow">НОВЕ СЛОВО</p><h1 className="session-word" lang="pl">{card.lemma_pl}</h1>
    <p className="session-pronunciation">{card.pronunciation_uk}{card.gender && genders[card.gender] ? ` · ${genders[card.gender]}` : ''}</p>
    <p className="session-translation">{card.translations_uk.join(', ')}</p>
    <div className="session-example"><p lang="pl">{card.example_pl}</p><p>{card.example_uk}</p></div>
    <div className="session-actions"><Button size="l" stretched disabled={pending} loading={pending} onClick={() => onAction('acknowledge')}>Далі</Button>
      <Button size="l" mode="plain" stretched disabled={pending} onClick={() => onAction('already-known')}>Вже знаю</Button></div>
  </>;
}
