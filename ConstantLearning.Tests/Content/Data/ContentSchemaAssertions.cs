using ConstantLearning.Platform.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Tests.Content.Data;

internal static class ContentSchemaAssertions
{
    public static async Task AssertAsync(AppDbContext db)
    {
        var columns = await db.Database.SqlQueryRaw<string>("""
            SELECT table_name || '.' || column_name || ':' || udt_name
                || COALESCE('(' || character_maximum_length || ')', '')
                || ':' || is_nullable || ':' || COALESCE(column_default, '-') AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name IN (
                'dictionary_entries', 'entry_versions', 'version_translations', 'version_accepted_answers')
            """).ToListAsync();
        columns.Should().BeEquivalentTo(new[]
        {
            "dictionary_entries.id:uuid:NO:-",
            "dictionary_entries.external_id:varchar(64):NO:-",
            "dictionary_entries.domain:varchar(16):NO:-",
            "dictionary_entries.published_version_id:uuid:YES:-",
            "dictionary_entries.course_position:int4:YES:-",
            "dictionary_entries.created_at:timestamptz:NO:now()",
            "entry_versions.id:uuid:NO:-",
            "entry_versions.entry_id:uuid:NO:-",
            "entry_versions.based_on_version_id:uuid:YES:-",
            "entry_versions.lemma_pl:varchar(200):NO:-",
            "entry_versions.part_of_speech:varchar(32):NO:-",
            "entry_versions.gender:varchar(8):YES:-",
            "entry_versions.gender_pair_id:varchar(64):YES:-",
            "entry_versions.aspect_pair_id:varchar(64):YES:-",
            "entry_versions.pronunciation_uk:varchar(200):NO:-",
            "entry_versions.example_pl:varchar(500):NO:-",
            "entry_versions.example_uk:varchar(500):NO:-",
            "entry_versions.show_context_before:bool:NO:-",
            "entry_versions.frequency_rank:int4:YES:-",
            "entry_versions.utility_rank:int4:YES:-",
            "entry_versions.legalization_stage:varchar(32):YES:-",
            "entry_versions.topic:varchar(32):YES:-",
            "entry_versions.validation_status:varchar(16):NO:-",
            "entry_versions.created_by_kind:varchar(24):NO:-",
            "entry_versions.content_report_id:uuid:YES:-",
            "entry_versions.content_hash:varchar(64):NO:-",
            "entry_versions.review_note:varchar(500):YES:-",
            "entry_versions.created_at:timestamptz:NO:now()",
            "version_translations.id:uuid:NO:-",
            "version_translations.version_id:uuid:NO:-",
            "version_translations.kind:varchar(16):NO:-",
            "version_translations.text_uk:varchar(200):NO:-",
            "version_translations.position:int2:NO:-",
            "version_translations.created_at:timestamptz:NO:now()",
            "version_accepted_answers.id:uuid:NO:-",
            "version_accepted_answers.version_id:uuid:NO:-",
            "version_accepted_answers.text_pl:varchar(200):NO:-",
            "version_accepted_answers.is_canonical:bool:NO:-",
            "version_accepted_answers.created_at:timestamptz:NO:now()"
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
                'dictionary_entries', 'entry_versions', 'version_translations', 'version_accepted_answers')
            """).ToListAsync();
        indexes.Should().BeEquivalentTo(new[]
        {
            "dictionary_entries_pkey:true:id:-",
            "entry_versions_pkey:true:id:-",
            "version_translations_pkey:true:id:-",
            "version_accepted_answers_pkey:true:id:-",
            "ux_dictionary_entries_external_id:true:external_id:-",
            "ix_dictionary_entries_course_position:false:course_position:(published_version_id IS NOT NULL)",
            "ix_dictionary_entries_published_version_id:false:published_version_id:-",
            "ix_entry_versions_entry_id:false:entry_id,created_at:-",
            "ix_entry_versions_based_on_version_id:false:based_on_version_id:-",
            "ix_entry_versions_content_report_id:false:content_report_id:-",
            "ux_version_translations_version_position:true:version_id,position:-",
            "ux_version_accepted_answers_version_text:true:version_id,text_pl:-"
        });
        var constraints = await db.Database.SqlQueryRaw<string>("""
            SELECT conname::text || ':' || contype::text || ':'
                || CASE WHEN contype = 'f' THEN confdeltype::text ELSE '-' END AS "Value"
            FROM pg_constraint
            WHERE conrelid IN ('dictionary_entries'::regclass, 'entry_versions'::regclass,
                'version_translations'::regclass, 'version_accepted_answers'::regclass)
              AND contype IN ('p', 'f', 'u', 'c')
            """).ToListAsync();
        constraints.Should().BeEquivalentTo(new[]
        {
            "dictionary_entries_pkey:p:-", "entry_versions_pkey:p:-",
            "version_translations_pkey:p:-", "version_accepted_answers_pkey:p:-",
            "entry_versions_entry_id_fkey:f:c", "entry_versions_based_on_version_id_fkey:f:a",
            "fk_dictionary_entries_published_version:f:a", "version_translations_version_id_fkey:f:c",
            "version_accepted_answers_version_id_fkey:f:c"
        });
    }
}
