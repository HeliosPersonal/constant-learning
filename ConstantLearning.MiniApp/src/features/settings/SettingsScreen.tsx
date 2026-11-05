import { useQuery } from '@tanstack/react-query';
import Settings from './Settings';
import { getLearnerAccess } from './api';
import type { Learner } from './model';

/** Екран налаштувань із входом до редактора: рядок показується лише користувачам із роллю редактора. */
export default function SettingsScreen({ learner, onBack, onStart, onOpenEditor }: { learner: Learner; onBack: (notice?: string) => void; onStart: () => void; onOpenEditor: () => void }) {
  const access = useQuery({ queryKey: ['learner', 'access'], queryFn: ({ signal }) => getLearnerAccess(signal), retry: false, refetchOnWindowFocus: false, refetchOnReconnect: false });
  // Ключ за активною мовою: після перемикання форма ліміту й нагадувань перечитується з даних нової мови (ADR-0005).
  return <Settings key={learner.active_target_language ?? 'none'} learner={learner} onBack={onBack} onStart={onStart} onOpenEditor={access.data?.content_editor ? onOpenEditor : undefined} />;
}
