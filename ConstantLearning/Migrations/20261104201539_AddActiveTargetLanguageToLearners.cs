using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveTargetLanguageToLearners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "active_target_language",
                table: "learners",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            // Беклфіл: наявні learner переходять на польську як активну мову.
            migrationBuilder.Sql(
                "UPDATE learners SET active_target_language = 'pl' WHERE active_target_language IS NULL;");

            // FK додаємо як NOT VALID + VALIDATE, щоб не тримати ACCESS EXCLUSIVE під час перевірки
            // наявних рядків (прецедент PersistSessionResultView).
            migrationBuilder.Sql("""
                ALTER TABLE learners
                    ADD CONSTRAINT learners_active_target_language_fkey
                    FOREIGN KEY (active_target_language) REFERENCES target_languages (code) NOT VALID;
                """);
            migrationBuilder.Sql(
                "ALTER TABLE learners VALIDATE CONSTRAINT learners_active_target_language_fkey;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE learners DROP CONSTRAINT IF EXISTS learners_active_target_language_fkey;");

            migrationBuilder.DropColumn(
                name: "active_target_language",
                table: "learners");
        }
    }
}
