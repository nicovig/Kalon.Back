using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalon.Back.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedContactsandContactLinkingMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactLinkingMode",
                table: "organizations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactLinkingNote",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MainContactId",
                table: "contacts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contacts_MainContactId",
                table: "contacts",
                column: "MainContactId");

            migrationBuilder.AddForeignKey(
                name: "FK_contacts_contacts_MainContactId",
                table: "contacts",
                column: "MainContactId",
                principalTable: "contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contacts_contacts_MainContactId",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_contacts_MainContactId",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "ContactLinkingMode",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "ContactLinkingNote",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "MainContactId",
                table: "contacts");
        }
    }
}
