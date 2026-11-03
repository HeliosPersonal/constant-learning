-- Зворотне заповнення: повертає у NULL лише рядки, які заповнив крок backfill (kind = 'plan').
-- WHERE захищає extra_word / extra_practice, якщо вони вже зʼявилися до часткового відкату.
UPDATE learning_sessions
SET kind = NULL
WHERE kind = 'plan';
