using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataSpecificationNavigatorBackend.Migrations
{
    /// <inheritdoc />
    public partial class ChangePKInSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PropertySuggestions",
                table: "PropertySuggestions");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "PropertySuggestions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_PropertySuggestions",
                table: "PropertySuggestions",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_PropertySuggestions_PropertyDataSpecificationId_SuggestedPropertyIri",
                table: "PropertySuggestions",
                columns: new[] { "PropertyDataSpecificationId", "SuggestedPropertyIri" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PropertySuggestions",
                table: "PropertySuggestions");

            migrationBuilder.DropIndex(
                name: "IX_PropertySuggestions_PropertyDataSpecificationId_SuggestedPropertyIri",
                table: "PropertySuggestions");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "PropertySuggestions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PropertySuggestions",
                table: "PropertySuggestions",
                columns: new[] { "PropertyDataSpecificationId", "SuggestedPropertyIri", "UserMessageId" });
        }
    }
}
