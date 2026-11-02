using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class PersistCurrentLearnerDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<LocalDate>(
                name: "current_day_date",
                table: "learners",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "current_day_ends_at",
                table: "learners",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "current_day_original_ends_at",
                table: "learners",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "current_day_starts_at",
                table: "learners",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "current_day_time_zone_at_start",
                table: "learners",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_day_date",
                table: "learners");

            migrationBuilder.DropColumn(
                name: "current_day_ends_at",
                table: "learners");

            migrationBuilder.DropColumn(
                name: "current_day_original_ends_at",
                table: "learners");

            migrationBuilder.DropColumn(
                name: "current_day_starts_at",
                table: "learners");

            migrationBuilder.DropColumn(
                name: "current_day_time_zone_at_start",
                table: "learners");
        }
    }
}
