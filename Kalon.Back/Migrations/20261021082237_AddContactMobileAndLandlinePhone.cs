using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalon.Back.Migrations
{
    /// <inheritdoc />
    public partial class AddContactMobileAndLandlinePhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "contacts",
                newName: "MobilePhone");

            migrationBuilder.AddColumn<string>(
                name: "LandlinePhone",
                table: "contacts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LandlinePhone",
                table: "contacts");

            migrationBuilder.RenameColumn(
                name: "MobilePhone",
                table: "contacts",
                newName: "Phone");
        }
    }
}
