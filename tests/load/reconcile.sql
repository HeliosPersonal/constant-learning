SELECT count(*) AS answer_events, count(DISTINCT session_item_id) AS distinct_answered_items,
       count(*) FILTER (WHERE session_item_id IS NULL) AS unlinked_events
FROM answer_events;
SELECT count(*) AS graded_items FROM session_items WHERE state = 'answered';
SELECT count(*) AS duplicated_items FROM
  (SELECT session_item_id FROM answer_events GROUP BY session_item_id HAVING count(*) > 1) repeated;
SELECT count(*) AS answered_without_event FROM session_items item
WHERE item.state = 'answered' AND NOT EXISTS (SELECT 1 FROM answer_events event WHERE event.session_item_id = item.id);
SELECT count(*) AS event_without_answered_item FROM answer_events event
WHERE NOT EXISTS (SELECT 1 FROM session_items item WHERE item.id = event.session_item_id AND item.state = 'answered');
SELECT status, count(*) FROM notification_dispatches WHERE kind = 'reminder' GROUP BY status ORDER BY status;
