// performance.timeOrigin починається з відкриття документа, ще до завантаження SDK і React.
export function createTodayTiming(now: () => number = () => performance.now()) {
  let reported = false;
  return {
    ready(send: (value: number) => Promise<void>) {
      if (reported) return;
      reported = true;
      const elapsed = now();
      // Контракт не приймає довші відкриття; не підміняємо їх обрізаним значенням.
      if (!Number.isFinite(elapsed) || elapsed < 0 || elapsed > 60_000) return;
      void send(Math.round(elapsed)).catch(() => { /* Телеметрія не перериває навчання й не повторюється. */ });
    },
  };
}

// Один екземпляр на документ: StrictMode, refetch і повторне монтування не дублюють подію.
export const todayTiming = createTodayTiming();
