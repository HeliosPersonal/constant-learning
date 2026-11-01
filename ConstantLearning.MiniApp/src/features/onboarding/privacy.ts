export interface PrivacyContact { url: string; label: string }

export function parsePrivacyContact(value: unknown): PrivacyContact | undefined {
  if (typeof value !== 'string' || !value.trim() || value.length > 2048) return;
  try {
    const url = new URL(value.trim());
    if (url.protocol === 'mailto:') {
      const email = decodeURIComponent(url.pathname);
      if (url.search || url.hash || !/^[^\s@<>:]+@[^\s@<>:]+\.[^\s@<>:]+$/.test(email)) return;
      return { url: url.href, label: email };
    }
    if (url.protocol === 'https:' && url.hostname && !url.username && !url.password) {
      return { url: url.href, label: `${url.hostname}${url.pathname === '/' ? '' : url.pathname}` };
    }
  } catch { /* Невалідний контакт не дозволяє завершити онбординг. */ }
}

export const configuredPrivacyContact = parsePrivacyContact(import.meta.env.VITE_PRIVACY_CONTACT_URL);
