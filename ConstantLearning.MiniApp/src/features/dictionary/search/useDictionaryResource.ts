import { useEffect, useState } from 'react';

type Load<T> = (signal: AbortSignal) => Promise<T>;
type Resource<T> = { key: string; load: Load<T>; status: 'ready'; data: T } | { key: string; load: Load<T>; status: 'error'; error: unknown };

export function useDictionaryResource<T>(key: string, load: Load<T> | null) {
  const [resource, setResource] = useState<Resource<T> | null>(null);
  useEffect(() => {
    if (!load) return;
    const controller = new AbortController();
    let current = true;
    void load(controller.signal).then(
      data => { if (current) setResource({ key, load, status: 'ready', data }); },
      error => { if (current) setResource({ key, load, status: 'error', error }); },
    );
    // Захист актуальності діє також для транспорту, який ігнорує AbortSignal.
    return () => { current = false; controller.abort(); };
  }, [key, load]);
  if (!load) return { status: 'idle' } as const;
  if (resource?.key !== key || resource.load !== load) return { status: 'pending' } as const;
  return resource;
}
