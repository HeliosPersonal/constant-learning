import { useState } from 'react';
import { Button, Card, Dialog, LearningProgress, Result, Spinner } from './ui';
import { useTheme } from '../theme/ThemeProvider';

export function ComponentPreview() {
  const { theme, setTheme } = useTheme();
  const [answer, setAnswer] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [state, setState] = useState<'ready' | 'loading' | 'error' | 'empty'>('ready');
  return <main className="app-shell">
    <header className="app-header"><a className="brand" href="/" aria-label="Constant Learning — головна">
      <span className="brand-icon" aria-hidden="true">cl.</span><span>Constant<br />Learning</span>
    </a><span className="eyebrow">ПОЛЬСЬКА ЩОДНЯ</span></header>

    <div className="intro"><p className="eyebrow">МАЛЕНЬКІ КРОКИ · СТАЛИЙ РУХ</p>
      <h1>Знайдіть свій ритм.</h1><p>Трохи польської. Щодня. У вашому темпі.</p></div>

    <Card aria-labelledby="welcome-title"><span className="card-index">01 / ВАШ ПРОСТІР</span>
      <h2 id="welcome-title">Місце для нових слів</h2><p className="muted">Короткі щоденні заняття допомагають запам’ятовувати нові слова й повертатися до знайомих.</p>
      <div className="empty-note"><span aria-hidden="true">↗</span><p>Кожне знайоме слово починається<br />з першої зустрічі.</p></div>
    </Card>

    <Card aria-labelledby="appearance-title"><div className="section-heading"><div><span className="card-index">02 / ВИГЛЯД</span>
      <h2 id="appearance-title">Комфорт у кожній темі</h2></div><span className="theme-symbol" aria-hidden="true">{theme === 'light' ? '☼' : '☾'}</span></div>
      <fieldset className="theme-options"><legend className="sr-only">Тема інтерфейсу</legend>
        {(['light', 'dark'] as const).map(option => <label className="theme-option" key={option}>
          <input type="radio" name="theme" checked={theme === option} onChange={() => setTheme(option)} />
          <span className="theme-option-text">{option === 'light' ? 'Світла' : 'Темна'}{theme === option && <span className="selected">Обрано ✓</span>}</span>
        </label>)}
      </fieldset><p className="caption">Вибір зберігається на цьому пристрої.</p>
      <Button mode="outline" size="l" stretched onClick={() => setDialogOpen(true)}>Про оформлення</Button>
    </Card>

    <details className="component-preview"><summary>Попередній перегляд компонентів</summary>
      <Card aria-labelledby="preview-title"><h2 id="preview-title">Картка та стани</h2>
        <p className="caption">Приклад інтерфейсу для локальної перевірки.</p>
        <LearningProgress value={1} total={5} />
        <p className="word" lang="pl">spokój</p><p className="muted">спокій</p>
        <label className="answer-label" htmlFor="answer">Ваш варіант польською</label>
        <input id="answer" value={answer} onChange={event => setAnswer(event.target.value)} placeholder="Введіть відповідь" autoComplete="off" aria-invalid={state === 'error'} aria-describedby={state === 'error' ? 'answer-error' : undefined} />
        {state === 'error' && <div id="answer-error"><Result tone="error">Не вдалося перевірити. Спробуйте ще раз.</Result></div>}
        {state === 'empty' && <p role="status" className="empty-state">Поки немає карток для повторення.</p>}
        {state === 'loading' && <div className="loading-state" role="status"><Spinner size="s" /> Завантажуємо картку…</div>}
        {state === 'ready' && <Result tone="success">Спільні компоненти готові до навчання.</Result>}
        <div className="preview-actions"><Button size="l" disabled>Перевірити відповідь</Button><Button size="l" mode="outline" onClick={() => setDialogOpen(true)}>Відкрити діалог</Button></div>
        <label className="state-label" htmlFor="preview-state">Стан прикладу</label>
        <select id="preview-state" value={state} onChange={event => setState(event.target.value as typeof state)}>
          <option value="ready">Готово</option><option value="loading">Завантаження</option><option value="error">Помилка</option><option value="empty">Порожньо</option>
        </select>
      </Card>
    </details>
    <footer>По слову ближче до впевненості.</footer>
    <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)}><p>Дві приглушені теми Everforest допомагають зосередитися на словах.</p><p className="muted">Вибір теми не змінює відповідь або поточну картку.</p></Dialog>
  </main>;
}
