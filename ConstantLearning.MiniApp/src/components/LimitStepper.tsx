import { DAILY_LIMIT_MAX, DAILY_LIMIT_MIN, newWordsLabel } from './LimitSlider';

/** Степер денного ліміту нових слів у стилі iOS; спільний для онбордингу й налаштувань. */
export function LimitStepper({ value, onChange, labelledBy, describedBy }: { value: number; onChange: (value: number) => void; labelledBy: string; describedBy?: string }) {
  return <div className="ios-stepper" role="group" aria-labelledby={labelledBy} aria-describedby={describedBy}>
    <button type="button" className="ios-plain" aria-label="Менше" disabled={value <= DAILY_LIMIT_MIN} onClick={() => onChange(value - 1)}>−</button>
    <output aria-live="polite">{value}<span className="sr-only"> {newWordsLabel(value)}</span></output>
    <button type="button" className="ios-plain" aria-label="Більше" disabled={value >= DAILY_LIMIT_MAX} onClick={() => onChange(value + 1)}>+</button>
  </div>;
}
