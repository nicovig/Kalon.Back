using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalon.Back.Migrations
{
    /// <inheritdoc />
    public partial class LinkUserandOrganizationandremoveStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contacts_OrganizationId_Status",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "contacts");

            migrationBuilder.AddColumn<bool>(
                name: "IsOut",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "contact_status_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    NewDurationDays = table.Column<int>(type: "integer", nullable: false),
                    ToRemindAfterMonths = table.Column<int>(type: "integer", nullable: false),
                    InactiveAfterMonths = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_status_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_status_settings_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contact_status_settings_OrganizationId",
                table: "contact_status_settings",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_status_settings");

            migrationBuilder.DropColumn(
                name: "IsOut",
                table: "contacts");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "contacts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_OrganizationId_Status",
                table: "contacts",
                columns: new[] { "OrganizationId", "Status" });
        }
    }
}
