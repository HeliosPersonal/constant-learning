using ConstantLearning.Platform.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Tests.Content.Data;

internal static class SearchJobsSchemaAssertions
{
    public static async Task AssertAsync(AppDbContext db)
    {
        var columns = await db.Database.SqlQueryRaw<string>("""
            SELECT table_name || '.' || column_name || ':' || udt_name
                || COALESCE('(' || character_maximum_length || ')', '')
                || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'public' AND (table_name IN (
                'search_forms', 'morph_forms', 'reference_entries', 'verification_jobs', 'notification_dispatches')
                OR (table_name = 'content_reports' AND column_name IN ('dedup_key', 'summary_dispatch_id')))
            """).ToListAsync();
        columns.Should().BeEquivalentTo(new[]
        {
            "search_forms.id:uuid:NO:-",
            "search_forms.entry_id:uuid:NO:-",
            "search_forms.version_id:uuid:NO:-",
            "search_forms.lang:varchar(2):NO:-",
            "search_forms.form_display:varchar(200):NO:-",
            "search_forms.form_folded:varchar(200):NO:-",
            "search_forms.is_lemma:bool:NO:-",
            "search_forms.created_at:timestamptz:NO:now()",
            "morph_forms.id:uuid:NO:-",
            "morph_forms.lang:varchar(2):NO:-",
            "morph_forms.source:varchar(32):NO:-",
            "morph_forms.lemma:varchar(200):NO:-",
            "morph_forms.form:varchar(200):NO:-",
            "morph_forms.tags:varchar(64):YES:-",
            "morph_forms.created_at:timestamptz:NO:now()",
            "reference_entries.id:uuid:NO:-",
            "reference_entries.source:varchar(32):NO:-",
            "reference_entries.lemma:varchar(200):NO:-",
            "reference_entries.lemma_folded:varchar(200):NO:-",
            "reference_entries.field:varchar(32):NO:-",
            "reference_entries.value:varchar(500):NO:-",
            "reference_entries.url:text:NO:-",
            "reference_entries.revision_id:varchar(32):YES:-",
            "reference_entries.retrieved_at:timestamptz:NO:-",
            "reference_entries.created_at:timestamptz:NO:now()",
            "verification_jobs.id:uuid:NO:-",
            "verification_jobs.content_report_id:uuid:NO:-",
            "verification_jobs.status:varchar(16):NO:-",
            "verification_jobs.attempts:int2:NO:-",
            "verification_jobs.next_attempt_at:timestamptz:NO:-",
            "verification_jobs.last_error:varchar(500):YES:-",
            "verification_jobs.claimed_at:timestamptz:YES:-",
            "verification_jobs.completed_at:timestamptz:YES:-",
            "verification_jobs.created_at:timestamptz:NO:now()",
            "notification_dispatches.id:uuid:NO:-",
            "notification_dispatches.recipient_telegram_user_id:int8:NO:-",
            "notification_dispatches.learner_id:uuid:YES:-",
            "notification_dispatches.kind:varchar(24):NO:-",
            "notification_dispatches.local_date:date:NO:-",
            "notification_dispatches.due_at:timestamptz:NO:-",
            "notification_dispatches.status:varchar(16):NO:-",
            "notification_dispatches.attempts:int2:NO:-",
            "notification_dispatches.claimed_at:timestamptz:YES:-",
            "notification_dispatches.sent_at:timestamptz:YES:-",
            "notification_dispatches.reason:varchar(200):YES:-",
            "notification_dispatches.created_at:timestamptz:NO:now()",
            "content_reports.dedup_key:varchar(64):YES:-",
            "content_reports.summary_dispatch_id:uuid:YES:-"
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
                'search_forms', 'morph_forms', 'reference_entries', 'verification_jobs',
                'notification_dispatches', 'content_reports', 'publication_log')
            """).ToListAsync();
        indexes.Should().BeEquivalentTo(new[]
        {
            "search_forms_pkey:true:id:-",
            "ix_search_forms_form_folded:false:form_folded:-",
            "ix_search_forms_entry_id:false:entry_id:-",
            "ix_search_forms_version_id:false:version_id:-",
            "morph_forms_pkey:true:id:-",
            "ix_morph_forms_lang_lemma:false:lang,lemma:-",
            "reference_entries_pkey:true:id:-",
            "ix_reference_entries_lookup:false:source,lemma_folded,field:-",
            "verification_jobs_pkey:true:id:-",
            "ux_verification_jobs_content_report:true:content_report_id:-",
            "ix_verification_jobs_status_next_attempt:false:status,next_attempt_at:-",
            "notification_dispatches_pkey:true:id:-",
            "ux_notification_dispatches_recipient_kind_date:true:recipient_telegram_user_id,kind,local_date:-",
            "ix_notification_dispatches_status_due:false:status,due_at:-",
            "ix_notification_dispatches_learner_id:false:learner_id:-",
            "content_reports_pkey:true:id:-",
            "ix_content_reports_learner_created:false:learner_id,created_at:-",
            "ix_content_reports_learner_unsummarized:false:learner_id,closed_at:((closed_at IS NOT NULL) AND (summary_dispatch_id IS NULL))",
            "ix_content_reports_status_created:false:status,created_at:-",
            "ix_content_reports_entry_id:false:entry_id:-",
            "ix_content_reports_version_id:false:version_id:-",
            "ux_content_reports_answer_event_id:true:answer_event_id:-",
            "ix_content_reports_resulting_version_id:false:resulting_version_id:-",
            "ux_content_reports_open_dedup:true:learner_id,dedup_key:((closed_at IS NULL) AND (dedup_key IS NOT NULL))",
            "ix_content_reports_summary_dispatch_id:false:summary_dispatch_id:-",
            "publication_log_pkey:true:id:-",
            "ix_publication_log_entry_created:false:entry_id,created_at:-",
            "ix_publication_log_actor_created:false:actor_kind,created_at:-",
            "ix_publication_log_from_version_id:false:from_version_id:-",
            "ix_publication_log_to_version_id:false:to_version_id:-",
            "ix_publication_log_actor_learner_id:false:actor_learner_id:-"
        });

        var opClass = await db.Database.SqlQueryRaw<string>("""
            SELECT operator_class.opcname AS "Value"
            FROM pg_index AS index_data
            JOIN pg_opclass AS operator_class ON operator_class.oid = index_data.indclass[0]
            WHERE index_data.indexrelid = 'ix_search_forms_form_folded'::regclass
            """).SingleAsync();
        opClass.Should().Be("text_pattern_ops");
        var constraints = await db.Database.SqlQueryRaw<string>("""
            SELECT conname::text || ':' || contype::text || ':'
                || CASE WHEN contype = 'f' THEN confdeltype::text ELSE '-' END AS "Value"
            FROM pg_constraint
            WHERE (conrelid IN ('search_forms'::regclass, 'morph_forms'::regclass,
                'reference_entries'::regclass, 'verification_jobs'::regclass, 'notification_dispatches'::regclass)
                OR (conrelid = 'content_reports'::regclass AND conname = 'content_reports_summary_dispatch_id_fkey'))
              AND contype IN ('p', 'f', 'u', 'c')
            """).ToListAsync();
        constraints.Should().BeEquivalentTo(new[]
        {
            "search_forms_pkey:p:-", "morph_forms_pkey:p:-", "reference_entries_pkey:p:-",
            "verification_jobs_pkey:p:-", "notification_dispatches_pkey:p:-",
            "search_forms_entry_id_fkey:f:c", "search_forms_version_id_fkey:f:c",
            "verification_jobs_content_report_id_fkey:f:c", "notification_dispatches_learner_id_fkey:f:c",
            "content_reports_summary_dispatch_id_fkey:f:a"
        });
    }
}
