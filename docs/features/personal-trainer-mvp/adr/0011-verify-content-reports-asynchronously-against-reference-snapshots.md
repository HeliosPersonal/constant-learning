---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0011 — Verify content reports asynchronously against reference snapshots

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

AC-35 автоматично застосовує зміну перекладу чи транскрипції, якщо довідкове джерело її підтверджує, без мовних моделей ([PRD §3](../PRD.md#3-non-goals)). Пілотна перевірка показала обмеження частоти запитів до Wikisłownik, а результат learner отримує лише наступного дня (AC-37).

## Decision drivers

- AC-35, AC-36: автоматична звірка лише для перекладу й транскрипції
- Non-goal: без мовних моделей ([PRD §3](../PRD.md#3-non-goals))
- Обмеження live-запитів Wikisłownik; джерельні знімки й атрибуція — `docs/dictionary/NOTICE.md`
- Ліцензії: Wikisłownik CC BY-SA 4.0, умови WSJP PAN невідомі

## Considered options

1. **Живі HTTP-запити під час надсилання** — Синхронна або негайна фонова звірка без кешу.
2. **Асинхронна звірка за локальними знімками** — Дамп Wikisłownik щомісяця в таблицю довідок, точкові запити до WSJP і урядових сторінок з обмеженням частоти й кешем; черга задач у PostgreSQL з пропуском заблокованих рядків.
3. **Лише ручний розгляд** — Усі звіти потрапляють у чергу content editor.

## Decision outcome

**Chosen:** Option 2, з ручним режимом як деградацією при недоступності джерела. Той самий шар знімків і валідаторів потрібен для дозвірки 2 000 лем перед запуском, а синхронність не дає користі, бо learner дізнається результат наступного дня.

## Consequences

**Positive**
- Один компонент для звірки звітів і передзапускової перевірки словника
- Доказ звірки зберігає URL і `revid`

**Negative**
- Знімок може бути до місяця застарілим
- Парсер вікітексту й конвертер IPA в кирилицю крихкі
- Частка автопідтверджень перекладу буде низькою: другого відкритого польсько-українського словника немає

**Neutral**
- Адаптери за портом `IReferenceSource`; збій джерела відправляє звіт у чергу з причиною

## Links

- PRD: [PRD](../PRD.md)
- SAD: [SAD](../sad.md) §4
- Related ADR: [ADR-0010](0010-version-dictionary-entries-in-postgres-with-a-published-pointer.md)
