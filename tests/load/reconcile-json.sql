SELECT json_build_object(
 'events', (SELECT count(*) FROM answer_events),
 'distinct_items', (SELECT count(DISTINCT session_item_id) FROM answer_events),
 'graded_items', (SELECT count(*) FROM session_items WHERE state = 'answered'),
 'unlinked', (SELECT count(*) FROM answer_events WHERE session_item_id IS NULL),
 'duplicates', (SELECT count(*) FROM (SELECT session_item_id FROM answer_events GROUP BY session_item_id HAVING count(*) > 1) x),
 'missing_event', (SELECT count(*) FROM session_items i WHERE i.state = 'answered' AND NOT EXISTS (SELECT 1 FROM answer_events e WHERE e.session_item_id = i.id)),
 'missing_item', (SELECT count(*) FROM answer_events e WHERE NOT EXISTS (SELECT 1 FROM session_items i WHERE i.id = e.session_item_id AND i.state = 'answered'))
);
