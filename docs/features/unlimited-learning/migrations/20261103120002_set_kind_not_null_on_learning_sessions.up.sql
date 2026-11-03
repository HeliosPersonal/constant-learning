-- Крок 3/3 (contract): виконувати після того, як застосунок пише kind у кожній session і backfill завершено.
ALTER TABLE learning_sessions
    ALTER COLUMN kind SET NOT NULL;
