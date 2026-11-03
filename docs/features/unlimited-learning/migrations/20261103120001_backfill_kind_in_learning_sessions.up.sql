-- Крок 2/3 (backfill): усі наявні session до цієї фічі є плановими.
UPDATE learning_sessions
SET kind = 'plan'
WHERE kind IS NULL;
