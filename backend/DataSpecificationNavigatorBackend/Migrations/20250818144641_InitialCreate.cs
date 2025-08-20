using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataSpecificationNavigatorBackend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataSpecifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OwlContent = table.Column<string>(type: "TEXT", nullable: false),
                    DataspecerPackageUuid = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSpecifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataSpecificationId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataSpecificationSubstructure = table.Column<string>(type: "json", nullable: false),
                    SuggestedMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_DataSpecifications_DataSpecificationId",
                        column: x => x.DataSpecificationId,
                        principalTable: "DataSpecifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataSpecificationItems",
                columns: table => new
                {
                    Iri = table.Column<string>(type: "TEXT", nullable: false),
                    DataSpecificationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                    DomainIri = table.Column<string>(type: "TEXT", nullable: true),
                    RangeDatatypeIri = table.Column<string>(type: "TEXT", nullable: true),
                    RangeIri = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSpecificationItems", x => new { x.DataSpecificationId, x.Iri });
                    table.ForeignKey(
                        name: "FK_DataSpecificationItems_DataSpecificationItems_DataSpecificationId_DomainIri",
                        columns: x => new { x.DataSpecificationId, x.DomainIri },
                        principalTable: "DataSpecificationItems",
                        principalColumns: new[] { "DataSpecificationId", "Iri" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DataSpecificationItems_DataSpecificationItems_DataSpecificationId_RangeIri",
                        columns: x => new { x.DataSpecificationId, x.RangeIri },
                        principalTable: "DataSpecificationItems",
                        principalColumns: new[] { "DataSpecificationId", "Iri" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DataSpecificationItems_DataSpecifications_DataSpecificationId",
                        column: x => x.DataSpecificationId,
                        principalTable: "DataSpecifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sender = table.Column<int>(type: "INTEGER", nullable: false),
                    TextContent = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                    PrecedingUserMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MappingText = table.Column<string>(type: "TEXT", nullable: true),
                    MappedItemsIri = table.Column<string>(type: "TEXT", nullable: true),
                    SuggestPropertiesText = table.Column<string>(type: "TEXT", nullable: true),
                    SuggestedPropertiesIri = table.Column<string>(type: "TEXT", nullable: true),
                    SparqlText = table.Column<string>(type: "TEXT", nullable: true),
                    SparqlQuery = table.Column<string>(type: "TEXT", nullable: true),
                    ReplyMessageId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Messages_PrecedingUserMessageId",
                        column: x => x.PrecedingUserMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Messages_ReplyMessageId",
                        column: x => x.ReplyMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConversationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SelectedPropertyIri = table.Column<string>(type: "TEXT", nullable: false),
                    IsOptional = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSelectTarget = table.Column<bool>(type: "INTEGER", nullable: false),
                    FilterExpression = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSelections_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemMappings",
                columns: table => new
                {
                    ItemDataSpecificationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemIri = table.Column<string>(type: "TEXT", nullable: false),
                    UserMessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MappedWords = table.Column<string>(type: "TEXT", nullable: false),
                    IsSelectTarget = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemMappings", x => new { x.ItemDataSpecificationId, x.ItemIri, x.UserMessageId });
                    table.ForeignKey(
                        name: "FK_ItemMappings_DataSpecificationItems_ItemDataSpecificationId_ItemIri",
                        columns: x => new { x.ItemDataSpecificationId, x.ItemIri },
                        principalTable: "DataSpecificationItems",
                        principalColumns: new[] { "DataSpecificationId", "Iri" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemMappings_Messages_UserMessageId",
                        column: x => x.UserMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertySuggestions",
                columns: table => new
                {
                    PropertyDataSpecificationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SuggestedPropertyIri = table.Column<string>(type: "TEXT", nullable: false),
                    UserMessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReasonForSuggestion = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertySuggestions", x => new { x.PropertyDataSpecificationId, x.SuggestedPropertyIri, x.UserMessageId });
                    table.ForeignKey(
                        name: "FK_PropertySuggestions_DataSpecificationItems_PropertyDataSpecificationId_SuggestedPropertyIri",
                        columns: x => new { x.PropertyDataSpecificationId, x.SuggestedPropertyIri },
                        principalTable: "DataSpecificationItems",
                        principalColumns: new[] { "DataSpecificationId", "Iri" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertySuggestions_Messages_UserMessageId",
                        column: x => x.UserMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_DataSpecificationId",
                table: "Conversations",
                column: "DataSpecificationId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSpecificationItems_DataSpecificationId_DomainIri",
                table: "DataSpecificationItems",
                columns: new[] { "DataSpecificationId", "DomainIri" });

            migrationBuilder.CreateIndex(
                name: "IX_DataSpecificationItems_DataSpecificationId_RangeIri",
                table: "DataSpecificationItems",
                columns: new[] { "DataSpecificationId", "RangeIri" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemMappings_UserMessageId",
                table: "ItemMappings",
                column: "UserMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_PrecedingUserMessageId",
                table: "Messages",
                column: "PrecedingUserMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyMessageId",
                table: "Messages",
                column: "ReplyMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertySuggestions_UserMessageId",
                table: "PropertySuggestions",
                column: "UserMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSelections_ConversationId",
                table: "UserSelections",
                column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemMappings");

            migrationBuilder.DropTable(
                name: "PropertySuggestions");

            migrationBuilder.DropTable(
                name: "UserSelections");

            migrationBuilder.DropTable(
                name: "DataSpecificationItems");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "DataSpecifications");
        }
    }
}
