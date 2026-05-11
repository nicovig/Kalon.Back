using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalon.Back.Migrations
{
    /// <inheritdoc />
    public partial class GeneratedDocumentDonationPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SnapshotDonationCount",
                table: "generated_documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SnapshotDonationToDate",
                table: "generated_documents",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotDonationCount",
                table: "generated_documents");

            migrationBuilder.DropColumn(
                name: "SnapshotDonationToDate",
                table: "generated_documents");
        }
    }
}
