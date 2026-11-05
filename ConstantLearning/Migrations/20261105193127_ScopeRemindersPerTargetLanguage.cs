using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <summary>
    /// Нагадування стають власними для кожної мови learner-а (ADR-0005): PK <c>reminder_schedules</c> переходить
    /// з (learner_id) на (learner_id, target_language), а <c>notification_dispatches</c> отримує мову і унікальність
    /// (отримувач, вид, дата, мова) з NULLS NOT DISTINCT, щоб сповіщення без мови лишалися унікальними.
    /// Існуючі нагадування переносяться на активну мову learner-а (інтерим «pl», якщо мови ще не обрано).
    /// </summary>
    /// <inheritdoc />
    public partial class ScopeRemindersPerTargetLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // === reminder_schedules ===
            // Крок 1: ADD nullable (зворотний ряд NOT NULL додається після backfill).
            migrationBuilder.Sql(
                "ALTER TABLE reminder_schedules ADD COLUMN IF NOT EXISTS target_language VARCHAR(2) NULL");

            // Крок 2: backfill з активної мови learner-а; без активної — інтерим 'pl'.
            migrationBuilder.Sql("""
                UPDATE reminder_schedules AS rs
                SET target_language = COALESCE(learners.active_target_language, 'pl')
                FROM learners
                WHERE learners.id = rs.learner_id AND rs.target_language IS NULL
                """);

            // Крок 3: SET NOT NULL після backfill.
            migrationBuilder.Sql(
                "ALTER TABLE reminder_schedules ALTER COLUMN target_language SET NOT NULL");

            // Крок 4: новий складений PK (learner_id, target_language) замість (learner_id).
            migrationBuilder.Sql(
                "ALTER TABLE reminder_schedules DROP CONSTRAINT IF EXISTS reminder_schedules_pkey");
            migrationBuilder.Sql(
                "ALTER TABLE reminder_schedules ADD CONSTRAINT reminder_schedules_pkey PRIMARY KEY (learner_id, target_language)");

            // Крок 5: FK на каталог мов (NOT VALID → VALIDATE, щоб не блокувати таблицю довгим скануванням).
            migrationBuilder.Sql("""
                ALTER TABLE reminder_schedules
                    ADD CONSTRAINT reminder_schedules_target_language_fkey
                    FOREIGN KEY (target_language) REFERENCES target_languages (code) NOT VALID
                """);
            migrationBuilder.Sql(
                "ALTER TABLE reminder_schedules VALIDATE CONSTRAINT reminder_schedules_target_language_fkey");

            // Крок 6: FK-індекс (CONCURRENTLY).
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_reminder_schedules_target_language ON reminder_schedules (target_language)",
                suppressTransaction: true);

            // === notification_dispatches ===
            // Крок 7: ADD nullable колонка мови (лише для нагадувань; інші види — NULL).
            migrationBuilder.Sql(
                "ALTER TABLE notification_dispatches ADD COLUMN IF NOT EXISTS target_language VARCHAR(2) NULL");

            // Крок 8: backfill мови для наявних нагадувань з активної мови learner-а.
            migrationBuilder.Sql("""
                UPDATE notification_dispatches AS nd
                SET target_language = COALESCE(learners.active_target_language, 'pl')
                FROM learners
                WHERE nd.kind = 'reminder' AND nd.target_language IS NULL AND learners.id = nd.learner_id
                """);

            // Крок 9–10: унікальність з мовою. Старий UNIQUE знімаємо, новий створюємо CONCURRENTLY з тим самим іменем.
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ux_notification_dispatches_recipient_kind_date",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_notification_dispatches_recipient_kind_date ON notification_dispatches (recipient_telegram_user_id, kind, local_date, target_language) NULLS NOT DISTINCT",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Реверс без втрати роботи: лишаємо лише нагадування активної мови (інші мови learner-а повертати нікуди).
            // Для notification_dispatches — видаляємо нагадування неактивних мов, інакше старий UNIQUE не створиться.
            migrationBuilder.Sql("""
                DELETE FROM notification_dispatches AS nd
                USING learners
                WHERE nd.kind = 'reminder' AND learners.id = nd.learner_id
                  AND nd.target_language IS DISTINCT FROM COALESCE(learners.active_target_language, 'pl')
                """);
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ux_notification_dispatches_recipient_kind_date",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_notification_dispatches_recipient_kind_date ON notification_dispatches (recipient_telegram_user_id, kind, local_date)",
                suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE notification_dispatches DROP COLUMN IF EXISTS target_language");

            migrationBuilder.Sql("""
                DELETE FROM reminder_schedules AS rs
                USING learners
                WHERE learners.id = rs.learner_id
                  AND rs.target_language IS DISTINCT FROM COALESCE(learners.active_target_language, 'pl')
                """);
            migrationBuilder.Sql(
                "ALTER TABLE reminder_schedules DROP CONSTRAINT IF EXISTS reminder_schedules_target_language_fkey");
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_reminder_schedules_target_language",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "ALTER TABLE reminder_schedules DROP CONSTRAINT IF EXISTS reminder_schedules_pkey");
            migrationBuilder.Sql("ALTER TABLE reminder_schedules DROP COLUMN IF EXISTS target_language");
            migrationBuilder.Sql(
                "ALTER TABLE reminder_schedules ADD CONSTRAINT reminder_schedules_pkey PRIMARY KEY (learner_id)");
        }
    }
}
