import { useId } from 'react';
import './limit-slider.css';

export const DAILY_LIMIT_MIN = 1;
export const DAILY_LIMIT_MAX = 10;
export const DAILY_LIMIT_LABEL = 'Денний ліміт нових слів';

// Форма слова «нове» залежить від числа: 1 — «слово», 2–4 — «слова», 5 і більше — «слів».
export function newWordsLabel(limit: number) {
  return limit === 1 ? 'нове слово' : limit <= 4 ? 'нові слова' : 'нових слів';
}

export function LimitSlider({ value, onChange, describedBy }: { value: number; onChange: (value: number) => void; describedBy?: string }) {
  const id = useId();
  const text = `${value} ${newWordsLabel(value)}`;
  return <div className="limit-slider">
    <output htmlFor={id} className="limit-slider-value"><strong>{value}</strong> {newWordsLabel(value)}</output>
    <input id={id} type="range" min={DAILY_LIMIT_MIN} max={DAILY_LIMIT_MAX} step={1} value={value} aria-label={DAILY_LIMIT_LABEL} aria-valuetext={text} aria-describedby={describedBy} onChange={event => onChange(Number(event.target.value))} />
    <div className="limit-slider-scale" aria-hidden="true"><span>{DAILY_LIMIT_MIN}</span><span>{DAILY_LIMIT_MAX}</span></div>
  </div>;
}
