using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class MigrateSessionsReportsTargetLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // === learning_sessions (кроки 22–26) ===
            // Крок 22: ADD nullable + backfill 'pl' (session не посилається на entry — мову не вивести).
            migrationBuilder.Sql(
                "ALTER TABLE learning_sessions ADD COLUMN IF NOT EXISTS target_language VARCHAR(2) NULL");
            migrationBuilder.Sql(
                "UPDATE learning_sessions SET target_language = 'pl' WHERE target_language IS NULL");
            migrationBuilder.Sql("""
                ALTER TABLE learning_sessions
                    ADD CONSTRAINT learning_sessions_target_language_fkey
                    FOREIGN KEY (target_language) REFERENCES target_languages (code) NOT VALID
                """);
            migrationBuilder.Sql(
                "ALTER TABLE learning_sessions VALIDATE CONSTRAINT learning_sessions_target_language_fkey");

            // Крок 23: SET NOT NULL після backfill.
            migrationBuilder.Sql(
                "ALTER TABLE learning_sessions ALTER COLUMN target_language SET NOT NULL");

            // Крок 24: FK-індекс (CONCURRENTLY).
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_learning_sessions_target_language ON learning_sessions (target_language)",
                suppressTransaction: true);

            // Кроки 25→26: своп UNIQUE однієї активної session під мовний вимір без вікна без обмеження —
            // спершу створити мово-свідомий UNIQUE, потім зняти глобальний. Обов'язкова передумова AC-04:
            // без цього друга мова не може мати активну session одночасно з першою.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_learning_sessions_one_active_lang ON learning_sessions (learner_id, target_language) WHERE status = 'active'",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ux_learning_sessions_one_active",
                suppressTransaction: true);

            // === content_reports (кроки 27–30) ===
            // Крок 27: ADD nullable + backfill 'pl'.
            migrationBuilder.Sql(
                "ALTER TABLE content_reports ADD COLUMN IF NOT EXISTS target_language VARCHAR(2) NULL");
            migrationBuilder.Sql(
                "UPDATE content_reports SET target_language = 'pl' WHERE target_language IS NULL");
            migrationBuilder.Sql("""
                ALTER TABLE content_reports
                    ADD CONSTRAINT content_reports_target_language_fkey
                    FOREIGN KEY (target_language) REFERENCES target_languages (code) NOT VALID
                """);
            migrationBuilder.Sql(
                "ALTER TABLE content_reports VALIDATE CONSTRAINT content_reports_target_language_fkey");

            // Крок 28: SET NOT NULL.
            migrationBuilder.Sql(
                "ALTER TABLE content_reports ALTER COLUMN target_language SET NOT NULL");

            // Кроки 29–30: FK-індекс + мово-свідома черга редактора (CONCURRENTLY).
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_content_reports_target_language ON content_reports (target_language)",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_content_reports_lang_status_created ON content_reports (target_language, status, created_at)",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // content_reports — реверс кроків 27–30.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_content_reports_lang_status_created");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_content_reports_target_language");
            migrationBuilder.Sql(
                "ALTER TABLE content_reports DROP CONSTRAINT IF EXISTS content_reports_target_language_fkey");
            migrationBuilder.Sql("ALTER TABLE content_reports DROP COLUMN IF EXISTS target_language");

            // learning_sessions — реверс кроків 22–26.
            // Відновлюємо глобальний UNIQUE однієї активної session — можливо ЛИШЕ якщо немає >1 активної
            // session на learner у різних мовах (інакше revert неможливий; у такому разі спершу закрити зайві).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_learning_sessions_one_active ON learning_sessions (learner_id) WHERE status = 'active'");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_learning_sessions_one_active_lang");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_learning_sessions_target_language");
            migrationBuilder.Sql(
                "ALTER TABLE learning_sessions DROP CONSTRAINT IF EXISTS learning_sessions_target_language_fkey");
            migrationBuilder.Sql("ALTER TABLE learning_sessions DROP COLUMN IF EXISTS target_language");
        }
    }
}
