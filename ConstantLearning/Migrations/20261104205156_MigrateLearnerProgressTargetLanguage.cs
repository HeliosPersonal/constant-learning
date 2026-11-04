using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class MigrateLearnerProgressTargetLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // === learner_words (кроки 13–17) ===
            // Крок 13: ADD nullable + backfill target_language з entry (ADR-0001, денормалізація).
            migrationBuilder.Sql(
                "ALTER TABLE learner_words ADD COLUMN IF NOT EXISTS target_language VARCHAR(2) NULL");
            migrationBuilder.Sql("""
                UPDATE learner_words lw SET target_language = de.target_language
                FROM dictionary_entries de
                WHERE de.id = lw.entry_id AND lw.target_language IS NULL
                """);
            migrationBuilder.Sql("""
                ALTER TABLE learner_words
                    ADD CONSTRAINT learner_words_target_language_fkey
                    FOREIGN KEY (target_language) REFERENCES target_languages (code) NOT VALID
                """);
            migrationBuilder.Sql(
                "ALTER TABLE learner_words VALIDATE CONSTRAINT learner_words_target_language_fkey");

            // Крок 14: SET NOT NULL після backfill.
            migrationBuilder.Sql(
                "ALTER TABLE learner_words ALTER COLUMN target_language SET NOT NULL");

            // Кроки 15–17: FK-індекс + мово-свідомі композити (CONCURRENTLY, по одному statement).
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_learner_words_target_language ON learner_words (target_language)",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_learner_words_learner_lang_due ON learner_words (learner_id, target_language, due_on)",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_learner_words_learner_lang_status ON learner_words (learner_id, target_language, status)",
                suppressTransaction: true);

            // === answer_events (кроки 18–21) ===
            // Крок 18: ADD nullable + backfill через entry_id.
            migrationBuilder.Sql(
                "ALTER TABLE answer_events ADD COLUMN IF NOT EXISTS target_language VARCHAR(2) NULL");
            migrationBuilder.Sql("""
                UPDATE answer_events ae SET target_language = de.target_language
                FROM dictionary_entries de
                WHERE de.id = ae.entry_id AND ae.target_language IS NULL
                """);
            migrationBuilder.Sql("""
                ALTER TABLE answer_events
                    ADD CONSTRAINT answer_events_target_language_fkey
                    FOREIGN KEY (target_language) REFERENCES target_languages (code) NOT VALID
                """);
            migrationBuilder.Sql(
                "ALTER TABLE answer_events VALIDATE CONSTRAINT answer_events_target_language_fkey");

            // Крок 19: SET NOT NULL.
            migrationBuilder.Sql(
                "ALTER TABLE answer_events ALTER COLUMN target_language SET NOT NULL");

            // Кроки 20–21: FK-індекс + мово-свідомий композит добового ліміту (CONCURRENTLY).
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_answer_events_target_language ON answer_events (target_language)",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_answer_events_learner_lang_day_outcome ON answer_events (learner_id, target_language, learner_day, outcome)",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // answer_events — реверс кроків 18–21.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_answer_events_learner_lang_day_outcome");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_answer_events_target_language");
            migrationBuilder.Sql(
                "ALTER TABLE answer_events DROP CONSTRAINT IF EXISTS answer_events_target_language_fkey");
            migrationBuilder.Sql("ALTER TABLE answer_events DROP COLUMN IF EXISTS target_language");

            // learner_words — реверс кроків 13–17.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_learner_words_learner_lang_status");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_learner_words_learner_lang_due");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_learner_words_target_language");
            migrationBuilder.Sql(
                "ALTER TABLE learner_words DROP CONSTRAINT IF EXISTS learner_words_target_language_fkey");
            migrationBuilder.Sql("ALTER TABLE learner_words DROP COLUMN IF EXISTS target_language");
        }
    }
}
