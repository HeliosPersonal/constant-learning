using ConstantLearning.Platform.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Tests.Content.Data;

internal static class ReportsPublicationSchemaAssertions
{
    public static async Task AssertAsync(AppDbContext db)
    {
        var columns = await db.Database.SqlQueryRaw<string>("""
            SELECT table_name || '.' || column_name || ':' || udt_name
                || COALESCE('(' || character_maximum_length || ')', '')
                || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name IN (
                'content_reports', 'answer_neutralizations', 'validation_evidence', 'publication_log')
            """).ToListAsync();
        columns.Should().BeEquivalentTo(new[]
        {
            "content_reports.id:uuid:NO:-",
            "content_reports.learner_id:uuid:NO:-",
            "content_reports.entry_id:uuid:YES:-",
            "content_reports.version_id:uuid:YES:-",
            "content_reports.answer_event_id:uuid:YES:-",
            "content_reports.origin:varchar(24):NO:-",
            "content_reports.field:varchar(32):YES:-",
            "content_reports.proposed_value:varchar(500):YES:-",
            "content_reports.comment:varchar(1000):YES:-",
            "content_reports.search_query:varchar(200):YES:-",
            "content_reports.status:varchar(24):NO:-",
            "content_reports.outcome:varchar(24):YES:-",
            "content_reports.rejection_reason:varchar(32):YES:-",
            "content_reports.resulting_version_id:uuid:YES:-",
            "content_reports.closed_at:timestamptz:YES:-",
            "content_reports.created_at:timestamptz:NO:now()",
            "answer_neutralizations.id:uuid:NO:-",
            "answer_neutralizations.answer_event_id:uuid:NO:-",
            "answer_neutralizations.content_report_id:uuid:NO:-",
            "answer_neutralizations.created_at:timestamptz:NO:now()",
            "validation_evidence.id:uuid:NO:-",
            "validation_evidence.version_id:uuid:NO:-",
            "validation_evidence.field:varchar(32):NO:-",
            "validation_evidence.source:varchar(32):NO:-",
            "validation_evidence.url:text:NO:-",
            "validation_evidence.revision_id:varchar(32):YES:-",
            "validation_evidence.attribution:varchar(200):YES:-",
            "validation_evidence.outcome:varchar(16):NO:-",
            "validation_evidence.retrieved_at:timestamptz:NO:-",
            "validation_evidence.created_at:timestamptz:NO:now()",
            "publication_log.id:uuid:NO:-",
            "publication_log.entry_id:uuid:NO:-",
            "publication_log.from_version_id:uuid:YES:-",
            "publication_log.to_version_id:uuid:YES:-",
            "publication_log.action:varchar(16):NO:-",
            "publication_log.actor_kind:varchar(24):NO:-",
            "publication_log.actor_learner_id:uuid:YES:-",
            "publication_log.created_at:timestamptz:NO:now()"
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
                'content_reports', 'answer_neutralizations', 'validation_evidence', 'publication_log')
            """).ToListAsync();
        indexes.Should().BeEquivalentTo(new[]
        {
            "content_reports_pkey:true:id:-",
            "ix_content_reports_learner_created:false:learner_id,created_at:-",
            "ix_content_reports_learner_closed:false:learner_id,closed_at:-",
            "ix_content_reports_status_created:false:status,created_at:-",
            "ix_content_reports_entry_id:false:entry_id:-",
            "ix_content_reports_version_id:false:version_id:-",
            "ix_content_reports_answer_event_id:false:answer_event_id:-",
            "ix_content_reports_resulting_version_id:false:resulting_version_id:-",
            "answer_neutralizations_pkey:true:id:-",
            "ux_answer_neutralizations_answer_event:true:answer_event_id:-",
            "ix_answer_neutralizations_content_report_id:false:content_report_id:-",
            "validation_evidence_pkey:true:id:-",
            "ix_validation_evidence_version_id:false:version_id:-",
            "publication_log_pkey:true:id:-",
            "ix_publication_log_entry_created:false:entry_id,created_at:-",
            "ix_publication_log_from_version_id:false:from_version_id:-",
            "ix_publication_log_to_version_id:false:to_version_id:-",
            "ix_publication_log_actor_learner_id:false:actor_learner_id:-"
        });

        var constraints = await db.Database.SqlQueryRaw<string>("""
            SELECT conname::text || ':' || contype::text || ':'
                || CASE WHEN contype = 'f' THEN confdeltype::text ELSE '-' END AS "Value"
            FROM pg_constraint
            WHERE (conrelid IN ('content_reports'::regclass, 'answer_neutralizations'::regclass,
                'validation_evidence'::regclass, 'publication_log'::regclass)
                OR (conrelid = 'entry_versions'::regclass AND conname = 'fk_entry_versions_content_report'))
              AND contype IN ('p', 'f', 'u', 'c')
            """).ToListAsync();
        constraints.Should().BeEquivalentTo(new[]
        {
            "content_reports_pkey:p:-", "answer_neutralizations_pkey:p:-",
            "validation_evidence_pkey:p:-", "publication_log_pkey:p:-",
            "content_reports_learner_id_fkey:f:c", "content_reports_entry_id_fkey:f:a",
            "content_reports_version_id_fkey:f:a", "content_reports_answer_event_id_fkey:f:a",
            "content_reports_resulting_version_id_fkey:f:a", "fk_entry_versions_content_report:f:a",
            "answer_neutralizations_answer_event_id_fkey:f:c",
            "answer_neutralizations_content_report_id_fkey:f:a",
            "validation_evidence_version_id_fkey:f:c", "publication_log_entry_id_fkey:f:c",
            "publication_log_from_version_id_fkey:f:a", "publication_log_to_version_id_fkey:f:a",
            "publication_log_actor_learner_id_fkey:f:a"
        });
    }
}
