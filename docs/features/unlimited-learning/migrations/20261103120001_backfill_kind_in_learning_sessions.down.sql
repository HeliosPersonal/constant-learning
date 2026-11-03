-- Зворотне заповнення: повертає kind у NULL (безпечно лише до появи extra-session).
UPDATE learning_sessions
SET kind = NULL;
