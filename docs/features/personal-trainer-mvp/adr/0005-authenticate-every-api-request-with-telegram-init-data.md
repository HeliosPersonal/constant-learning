---
status: Accepted
owner: "Viacheslav Melnichenko"
reviewers: ["Tech Lead"]
updated_at: "2026-10-31"
feature_size: L
stage: "04-05"
ticket: "TBD"
---

# 0005 — Authenticate every API request with Telegram initData

- **Status:** Accepted
- **Date:** 2026-10-31
- **Deciders:** Viacheslav Melnichenko (рекомендація агентів-експертів, режим «автономно + експерти»)

## Context

PRD §6.1 вимагає ідентифікувати learner за перевіреною Telegram-ідентичністю й відмовляти при підробці. Mini App отримує від Telegram підписані `initData`. Треба вирішити, як бекенд автентифікує кожен запит.

## Decision drivers

- Abuse case «підробка ідентичності» (PRD §6.1)
- AC-29, AC-41: доступ лише до власних даних і до черги лише для content editor
- WebView Telegram на iOS обмежує cookie
- Latency p95 оцінки відповіді ≤ 500 ms (PRD §6)

## Considered options

1. **`initData` у кожному запиті** — Заголовок `Authorization: tma <initData>`, власний `AuthenticationHandler`: HMAC-SHA256 з ключем від bot token, `auth_date` ≤ 24 год, claims `learner_id` і `telegram_user_id`.
2. **Обмін на короткоживучий JWT** — Одноразова перевірка `initData`, далі Bearer-токен з refresh.
3. **Обмін на cookie-сесію** — Одноразова перевірка, далі cookie-автентифікація ASP.NET Core.

## Decision outcome

**Chosen:** Option 1. Без стану, без сховища сесій і ключа підпису, без залежності від cookie у WebView; HMAC коштує мікросекунди й не впливає на latency.

## Consequences

**Positive**
- Однакова перевірка для learner і content editor
- `learner_id` лише з claims, чужі ресурси повертаються як «не знайдено»

**Negative**
- Перехоплений `initData` чинний до 24 год; заголовок не логується
- Після 24 год відкритої Mini App потрібне перевідкриття
- Ротація bot token інвалідує відкриті Mini App
- Валідатор пишемо самі й покриваємо тестами з відомими векторами

**Neutral**
- Перехід на JWT пізніше — 1–2 дні, клієнт міняє лише заголовок

## Links

- PRD: [[../PRD.md]]
- SAD: [[../sad.md]] §8
- Related ADR: [[0002-build-mini-app-as-react-spa-served-by-aspnet-core]]
