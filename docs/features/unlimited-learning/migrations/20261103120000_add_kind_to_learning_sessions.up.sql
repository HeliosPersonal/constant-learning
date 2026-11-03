-- Крок 1/3 (expand): kind зі значеннями plan / extra_word / extra_practice; колонка nullable, застосунок одразу пише kind у нових session.
ALTER TABLE learning_sessions
    ADD COLUMN IF NOT EXISTS kind VARCHAR(24) NULL;
