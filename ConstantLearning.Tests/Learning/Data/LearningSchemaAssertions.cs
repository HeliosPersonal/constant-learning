using ConstantLearning.Platform.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Tests.Learning.Data;

internal static class LearningSchemaAssertions
{
    public static async Task AssertAsync(AppDbContext db)
    {
        var columns = await db.Database.SqlQueryRaw<string>("""
            SELECT table_name || '.' || column_name || ':' || udt_name
                || COALESCE('(' || character_maximum_length || ')', '')
                || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name IN (
                'learner_words', 'learning_sessions', 'session_items', 'answer_events')
            """).ToListAsync();
        columns.Should().BeEquivalentTo(new[]
        {
            "learner_words.id:uuid:NO:-",
            "learner_words.learner_id:uuid:NO:-",
            "learner_words.entry_id:uuid:NO:-",
            "learner_words.status:varchar(16):NO:-",
            "learner_words.consecutive_scheduled_correct:int2:NO:-",
            "learner_words.first_shown_day:date:YES:-",
            "learner_words.last_review_day:date:YES:-",
            "learner_words.interval_days:int4:YES:-",
            "learner_words.due_on:date:YES:-",
            "learner_words.control_pending:bool:NO:-",
            "learner_words.lapses:int4:NO:-",
            "learner_words.model_state:jsonb:NO:-",
            "learner_words.scheduler_version:varchar(16):NO:-",
            "learner_words.created_at:timestamptz:NO:now()",
            "learning_sessions.id:uuid:NO:-",
            "learning_sessions.learner_id:uuid:NO:-",
            "learning_sessions.learner_day:date:NO:-",
            "learning_sessions.ends_at:timestamptz:NO:-",
            "learning_sessions.status:varchar(16):NO:-",
            "learning_sessions.last_activity_at:timestamptz:YES:-",
            "learning_sessions.completed_at:timestamptz:YES:-",
            "learning_sessions.created_at:timestamptz:NO:now()",
            "session_items.id:uuid:NO:-",
            "session_items.session_id:uuid:NO:-",
            "session_items.entry_id:uuid:NO:-",
            "session_items.position:int2:NO:-",
            "session_items.kind:varchar(24):NO:-",
            "session_items.state:varchar(16):NO:-",
            "session_items.answer_payload_hash:varchar(64):YES:-",
            "session_items.answered_at:timestamptz:YES:-",
            "session_items.created_at:timestamptz:NO:now()",
            "answer_events.id:uuid:NO:-",
            "answer_events.learner_id:uuid:NO:-",
            "answer_events.entry_id:uuid:NO:-",
            "answer_events.version_id:uuid:NO:-",
            "answer_events.session_item_id:uuid:YES:-",
            "answer_events.exercise_kind:varchar(24):NO:-",
            "answer_events.outcome:varchar(24):NO:-",
            "answer_events.raw_answer:varchar(200):YES:-",
            "answer_events.normalized_answer:varchar(200):YES:-",
            "answer_events.answered_at:timestamptz:NO:-",
            "answer_events.learner_day:date:NO:-",
            "answer_events.time_zone:varchar(64):NO:-",
            "answer_events.due_on:date:YES:-",
            "answer_events.interval_before:int4:YES:-",
            "answer_events.scheduler_version:varchar(16):NO:-",
            "answer_events.state_before:jsonb:NO:-",
            "answer_events.created_at:timestamptz:NO:now()"
        });

        var indexes = await db.Database.SqlQueryRaw<string>("""
            SELECT index_relation.relname || ':' || index_data.indisunique::text || ':'
                || (SELECT string_agg(attribute.attname, ',' ORDER BY key.position)
                    FROM unnest(index_data.indkey) WITH ORDINALITY AS key(number, position)
                    JOIN pg_attribute AS attribute
                      ON attribute.attrelid = table_relation.oid AND attribute.attnum = key.number)
                || ':' || COALESCE(pg_get_expr(index_data.indpred, index_data.indrelid), '-') AS "Value"
            FROM pg_index AS index_data
            JOIN pg_class AS index_relation ON index_relation.oid = index_data.indexrelid
            JOIN pg_class AS table_relation ON table_relation.oid = index_data.indrelid
            WHERE table_relation.relnamespace = 'public'::regnamespace AND table_relation.relname IN (
                'learner_words', 'learning_sessions', 'session_items', 'answer_events')
            """).ToListAsync();
        indexes.Should().BeEquivalentTo(new[]
        {
            "learner_words_pkey:true:id:-",
            "ux_learner_words_learner_entry:true:learner_id,entry_id:-",
            "ix_learner_words_learner_due:false:learner_id,due_on:-",
            "ix_learner_words_learner_status:false:learner_id,status:-",
            "ix_learner_words_entry_id:false:entry_id:-",
            "learning_sessions_pkey:true:id:-",
            "ux_learning_sessions_one_active:true:learner_id:((status)::text = 'active'::text)",
            "ix_learning_sessions_learner_completed:false:learner_id,completed_at:-",
            "ix_learning_sessions_learner_day:false:learner_id,learner_day:-",
            "session_items_pkey:true:id:-",
            "ux_session_items_session_position:true:session_id,position:-",
            "ix_session_items_entry_id:false:entry_id:-",
            "answer_events_pkey:true:id:-",
            "ux_answer_events_session_item:true:session_item_id:-",
            "ix_answer_events_learner_entry:false:learner_id,entry_id,answered_at:-",
            "ix_answer_events_learner_day_outcome:false:learner_id,learner_day,outcome:-",
            "ix_answer_events_entry_id:false:entry_id:-",
            "ix_answer_events_version_id:false:version_id:-"
        });

        var constraints = await db.Database.SqlQueryRaw<string>("""
            SELECT conname::text || ':' || contype::text || ':'
                || CASE WHEN contype = 'f' THEN confdeltype::text ELSE '-' END AS "Value"
            FROM pg_constraint
            WHERE conrelid IN ('learner_words'::regclass, 'learning_sessions'::regclass,
                'session_items'::regclass, 'answer_events'::regclass)
              AND contype IN ('p', 'f', 'u', 'c')
            """).ToListAsync();
        constraints.Should().BeEquivalentTo(new[]
        {
            "learner_words_pkey:p:-", "learning_sessions_pkey:p:-",
            "session_items_pkey:p:-", "answer_events_pkey:p:-",
            "learner_words_learner_id_fkey:f:c", "learner_words_entry_id_fkey:f:a",
            "learning_sessions_learner_id_fkey:f:c",
            "session_items_session_id_fkey:f:c", "session_items_entry_id_fkey:f:a",
            "answer_events_learner_id_fkey:f:c", "answer_events_entry_id_fkey:f:a",
            "answer_events_version_id_fkey:f:a", "answer_events_session_item_id_fkey:f:n"
        });
    }
}
