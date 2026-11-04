using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class CreateTargetLanguages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "target_languages",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    display_name_uk = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    normalization_profile = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("target_languages_pkey", x => x.code);
                });

            // Seed каталогу: pl увімкнено (профіль 'pl'); en/de/nl вимкнено, профілі заведено одразу
            // (правила кодуються в T07a/T07b після spike OQ-7). ON CONFLICT — ідемпотентний повтор.
            migrationBuilder.Sql("""
                INSERT INTO target_languages (code, display_name_uk, is_enabled, sort_order, normalization_profile) VALUES
                    ('pl', 'Польська',      TRUE,  1, 'pl'),
                    ('en', 'Англійська',    FALSE, 2, 'en'),
                    ('de', 'Німецька',      FALSE, 3, 'de'),
                    ('nl', 'Нідерландська', FALSE, 4, 'nl')
                ON CONFLICT (code) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM target_languages WHERE code IN ('pl', 'en', 'de', 'nl');");
            migrationBuilder.DropTable(
                name: "target_languages");
        }
    }
}
