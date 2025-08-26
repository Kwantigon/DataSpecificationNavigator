using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataSpecificationNavigatorBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnotationAndComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwlAnnotation",
                table: "DataSpecificationItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RdfsComment",
                table: "DataSpecificationItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwlAnnotation",
                table: "DataSpecificationItems");

            migrationBuilder.DropColumn(
                name: "RdfsComment",
                table: "DataSpecificationItems");
        }
    }
}
