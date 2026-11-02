export function isTimeZone(value: string): boolean {
  if (!value || value.length > 64) return false;
  try { new Intl.DateTimeFormat('uk-UA', { timeZone: value }).format(); return true; }
  catch { return false; }
}

export function detectTimeZone(): string | undefined {
  try {
    const zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    return isTimeZone(zone) ? zone : undefined;
  } catch { return undefined; }
}

export function timeZoneOptions(current?: string): string[] {
  const supported = Intl as typeof Intl & { supportedValuesOf?: (key: string) => string[] };
  let zones: string[] = [];
  try { zones = supported.supportedValuesOf?.('timeZone') ?? []; } catch { /* Старі WebView використовують короткий список. */ }
  return [...new Set([current, 'Europe/Warsaw', 'Europe/Kyiv', 'Europe/London', 'Europe/Berlin', 'America/New_York', 'Asia/Tokyo', 'UTC', ...zones].filter((zone): zone is string => !!zone))].sort();
}
