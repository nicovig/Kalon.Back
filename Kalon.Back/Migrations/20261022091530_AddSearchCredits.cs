using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalon.Back.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "search_credit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SearchCreditId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "text", nullable: false),
                    CreditsConsumed = table.Column<decimal>(type: "numeric", nullable: false),
                    Siren = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_credit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "search_credits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CreditsGranted = table.Column<decimal>(type: "numeric", nullable: false),
                    CreditsUsed = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_credits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_search_credits_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_search_credit_logs_OrganizationId",
                table: "search_credit_logs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_search_credit_logs_OrganizationId_CreatedAt",
                table: "search_credit_logs",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_search_credit_logs_SearchCreditId",
                table: "search_credit_logs",
                column: "SearchCreditId");

            migrationBuilder.CreateIndex(
                name: "IX_search_credits_OrganizationId",
                table: "search_credits",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_search_credits_OrganizationId_CreatedAt",
                table: "search_credits",
                columns: new[] { "OrganizationId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_credit_logs");

            migrationBuilder.DropTable(
                name: "search_credits");
        }
    }
}
