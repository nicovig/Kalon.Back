using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalon.Back.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotaUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quota_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotaType = table.Column<string>(type: "text", nullable: false),
                    Period = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quota_usages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quota_usages_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quota_usages_OrganizationId",
                table: "quota_usages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_quota_usages_OrganizationId_QuotaType_Period",
                table: "quota_usages",
                columns: new[] { "OrganizationId", "QuotaType", "Period" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quota_usages");
        }
    }
}
