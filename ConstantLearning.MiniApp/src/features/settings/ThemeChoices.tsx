import { useTheme } from '../../theme/ThemeProvider';

export function ThemeChoices({ group = 'settings-theme' }: { group?: string }) {
  const { theme, setTheme } = useTheme();
  return <div className="ios-segmented" role="radiogroup" aria-label="Тема інтерфейсу">
    {(['light', 'dark'] as const).map(option => <label className={`ios-segment${theme === option ? ' ios-segment--on' : ''}`} key={option}>
      <input type="radio" name={group} checked={theme === option} onChange={() => setTheme(option)} />
      {option === 'light' ? 'Світла' : 'Темна'}
    </label>)}
  </div>;
}
