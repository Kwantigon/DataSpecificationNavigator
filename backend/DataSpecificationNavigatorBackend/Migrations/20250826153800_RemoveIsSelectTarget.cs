using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataSpecificationNavigatorBackend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsSelectTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSelectTarget",
                table: "UserSelections");

            migrationBuilder.DropColumn(
                name: "IsSelectTarget",
                table: "ItemMappings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSelectTarget",
                table: "UserSelections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSelectTarget",
                table: "ItemMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
