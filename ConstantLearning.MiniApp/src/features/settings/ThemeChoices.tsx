import { useTheme } from '../../theme/ThemeProvider';

export function ThemeChoices({ group = 'settings-theme' }: { group?: string }) {
  const { theme, setTheme } = useTheme();
  return <fieldset className="theme-options"><legend className="sr-only">Тема інтерфейсу</legend>
    {(['light', 'dark'] as const).map(option => <label className="theme-option" key={option}>
      <input type="radio" name={group} checked={theme === option} onChange={() => setTheme(option)} />
      <span className="theme-option-text">{option === 'light' ? 'Світла' : 'Темна'}{theme === option && <span className="selected">Обрано ✓</span>}</span>
    </label>)}
  </fieldset>;
}
