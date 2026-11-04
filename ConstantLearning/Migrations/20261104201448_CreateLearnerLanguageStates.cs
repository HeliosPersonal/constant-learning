using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class CreateLearnerLanguageStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learner_language_states",
                columns: table => new
                {
                    learner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    daily_new_words_limit = table.Column<short>(type: "smallint", nullable: false),
                    pending_daily_new_words_limit = table.Column<short>(type: "smallint", nullable: true),
                    pending_limit_effective_from = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    new_words_available_from = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    current_day_date = table.Column<LocalDate>(type: "date", nullable: true),
                    current_day_starts_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    current_day_ends_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    current_day_original_ends_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    current_day_time_zone_at_start = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    course_completed_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("learner_language_states_pkey", x => new { x.learner_id, x.target_language });
                    table.ForeignKey(
                        name: "learner_language_states_learner_id_fkey",
                        column: x => x.learner_id,
                        principalTable: "learners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "learner_language_states_target_language_fkey",
                        column: x => x.target_language,
                        principalTable: "target_languages",
                        principalColumn: "code");
                });

            migrationBuilder.CreateIndex(
                name: "ix_learner_language_states_target_language",
                table: "learner_language_states",
                column: "target_language");

            // Беклфіл: кожному наявному learner — рядок стану для польської (чинна єдина мова).
            migrationBuilder.Sql("""
                INSERT INTO learner_language_states (
                    learner_id, target_language, daily_new_words_limit, pending_daily_new_words_limit,
                    pending_limit_effective_from, new_words_available_from, current_day_date,
                    current_day_starts_at, current_day_ends_at, current_day_original_ends_at,
                    current_day_time_zone_at_start, course_completed_at)
                SELECT
                    id, 'pl', daily_new_words_limit, pending_daily_new_words_limit,
                    pending_limit_effective_from, new_words_available_from, current_day_date,
                    current_day_starts_at, current_day_ends_at, current_day_original_ends_at,
                    current_day_time_zone_at_start, course_completed_at
                FROM learners
                ON CONFLICT (learner_id, target_language) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM learner_language_states WHERE target_language = 'pl';");

            migrationBuilder.DropTable(
                name: "learner_language_states");
        }
    }
}
