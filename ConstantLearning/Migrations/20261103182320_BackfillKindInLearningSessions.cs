using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class BackfillKindInLearningSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Крок 2/3 (backfill) unlimited-learning: усі наявні session до цієї фічі — планові.
            // Таблиця мала (пілот ≤ 100 learner), пакетування не потрібне.
            migrationBuilder.Sql("UPDATE learning_sessions SET kind = 'plan' WHERE kind IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Зворотний крок повертає колонку в стан після expand (усі значення знову NULL).
            migrationBuilder.Sql("UPDATE learning_sessions SET kind = NULL WHERE kind = 'plan';");
        }
    }
}
