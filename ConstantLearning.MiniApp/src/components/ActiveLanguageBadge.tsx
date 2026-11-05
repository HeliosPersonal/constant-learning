import type { Learner } from '../features/onboarding/api';

const languageNames: Record<string, string> = { pl: 'Польська', en: 'Англійська', de: 'Німецька', nl: 'Нідерландська' };

export function ActiveLanguageBadge({ learner }: { learner: Learner }) {
  const language = learner.active_target_language;
  if (!language) return null;
  return <div className="active-language"><span>{languageNames[language] ?? language.toUpperCase()}</span></div>;
}
