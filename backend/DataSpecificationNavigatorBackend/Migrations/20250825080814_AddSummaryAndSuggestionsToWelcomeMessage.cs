using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataSpecificationNavigatorBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSummaryAndSuggestionsToWelcomeMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataSpecificationSummary",
                table: "Messages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedClasses",
                table: "Messages",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataSpecificationSummary",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SuggestedClasses",
                table: "Messages");
        }
    }
}
