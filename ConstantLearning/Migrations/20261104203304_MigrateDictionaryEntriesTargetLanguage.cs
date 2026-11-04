using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class MigrateDictionaryEntriesTargetLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Крок 1 (ADR-0003): ADD nullable + backfill 'pl' наявним записам.
            migrationBuilder.Sql(
                "ALTER TABLE dictionary_entries ADD COLUMN IF NOT EXISTS target_language VARCHAR(2) NULL");
            migrationBuilder.Sql(
                "UPDATE dictionary_entries SET target_language = 'pl' WHERE target_language IS NULL");

            // Крок 2: FK на каталог мов через NOT VALID + VALIDATE (без тривалого AccessExclusive).
            migrationBuilder.Sql("""
                ALTER TABLE dictionary_entries
                    ADD CONSTRAINT dictionary_entries_target_language_fkey
                    FOREIGN KEY (target_language) REFERENCES target_languages (code) NOT VALID
                """);
            migrationBuilder.Sql(
                "ALTER TABLE dictionary_entries VALIDATE CONSTRAINT dictionary_entries_target_language_fkey");

            // Крок 3 з 3: SET NOT NULL після повного backfill.
            migrationBuilder.Sql(
                "ALTER TABLE dictionary_entries ALTER COLUMN target_language SET NOT NULL");

            // Крок 4: новий унікальний індекс у межах мови (CONCURRENTLY, один statement) — створюємо ДО дропу старого.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_dictionary_entries_lang_external_id ON dictionary_entries (target_language, external_id)",
                suppressTransaction: true);

            // Крок 5: дроп глобального унікального — вікна без унікальності немає.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_dictionary_entries_external_id");

            // Крок 6: новий partial-індекс впорядкування курсу per-language (CONCURRENTLY, один statement) — ДО дропу старого.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_dictionary_entries_lang_course_position ON dictionary_entries (target_language, course_position) WHERE published_version_id IS NOT NULL",
                suppressTransaction: true);

            // Крок 7: дроп старого partial-індексу — вікна без впорядкування немає.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_dictionary_entries_course_position");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Реверс у зворотному порядку: спершу відновлюємо глобальні індекси, тоді прибираємо мовні.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_dictionary_entries_course_position ON dictionary_entries (course_position) WHERE published_version_id IS NOT NULL");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_dictionary_entries_lang_course_position");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_dictionary_entries_external_id ON dictionary_entries (external_id)");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_dictionary_entries_lang_external_id");

            migrationBuilder.Sql(
                "ALTER TABLE dictionary_entries ALTER COLUMN target_language DROP NOT NULL");
            migrationBuilder.Sql(
                "ALTER TABLE dictionary_entries DROP CONSTRAINT IF EXISTS dictionary_entries_target_language_fkey");
            migrationBuilder.Sql("ALTER TABLE dictionary_entries DROP COLUMN IF EXISTS target_language");
        }
    }
}
