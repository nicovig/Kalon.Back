using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalon.Back.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContactKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contacts_OrganizationId_Kind",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "contacts");

            migrationBuilder.AddColumn<bool>(
                name: "IsEnterprise",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEnterprise",
                table: "contacts");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "contacts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_OrganizationId_Kind",
                table: "contacts",
                columns: new[] { "OrganizationId", "Kind" });
        }
    }
}
