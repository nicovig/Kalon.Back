using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalon.Back.Migrations
{
    /// <inheritdoc />
    public partial class GeneratedDocumentandMailLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_logs");

            migrationBuilder.DropTable(
                name: "tax_receipts");

            migrationBuilder.AddColumn<Guid>(
                name: "GeneratedDocumentId",
                table: "donations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "generated_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: true),
                    TaxReductionRate = table.Column<decimal>(type: "numeric", nullable: true),
                    SnapshotOrgName = table.Column<string>(type: "text", nullable: false),
                    SnapshotOrgRna = table.Column<string>(type: "text", nullable: true),
                    SnapshotOrgSiret = table.Column<string>(type: "text", nullable: true),
                    SnapshotOrgFiscalStatus = table.Column<string>(type: "text", nullable: true),
                    SnapshotOrgStreet = table.Column<string>(type: "text", nullable: true),
                    SnapshotOrgPostalCode = table.Column<string>(type: "text", nullable: true),
                    SnapshotOrgCity = table.Column<string>(type: "text", nullable: true),
                    SnapshotContactDisplayName = table.Column<string>(type: "text", nullable: false),
                    SnapshotContactAddress = table.Column<string>(type: "text", nullable: true),
                    SnapshotContactSiret = table.Column<string>(type: "text", nullable: true),
                    SnapshotAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    SnapshotDonationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SnapshotDonationType = table.Column<string>(type: "text", nullable: false),
                    SignatureImagePath = table.Column<string>(type: "text", nullable: true),
                    PdfPath = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentToEmail = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SendError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_generated_documents_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mail_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEmail = table.Column<bool>(type: "boolean", nullable: false),
                    SentToEmail = table.Column<string>(type: "text", nullable: true),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    PrintedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MailedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mail_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mail_logs_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mail_logs_generated_documents_GeneratedDocumentId",
                        column: x => x.GeneratedDocumentId,
                        principalTable: "generated_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_mail_logs_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_donations_GeneratedDocumentId",
                table: "donations",
                column: "GeneratedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_generated_documents_OrganizationId_DocumentType",
                table: "generated_documents",
                columns: new[] { "OrganizationId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_generated_documents_OrganizationId_OrderNumber",
                table: "generated_documents",
                columns: new[] { "OrganizationId", "OrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_generated_documents_OrganizationId_Status",
                table: "generated_documents",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_mail_logs_ContactId",
                table: "mail_logs",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_mail_logs_GeneratedDocumentId",
                table: "mail_logs",
                column: "GeneratedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_mail_logs_OrganizationId",
                table: "mail_logs",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_donations_generated_documents_GeneratedDocumentId",
                table: "donations",
                column: "GeneratedDocumentId",
                principalTable: "generated_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_donations_generated_documents_GeneratedDocumentId",
                table: "donations");

            migrationBuilder.DropTable(
                name: "mail_logs");

            migrationBuilder.DropTable(
                name: "generated_documents");

            migrationBuilder.DropIndex(
                name: "IX_donations_GeneratedDocumentId",
                table: "donations");

            migrationBuilder.DropColumn(
                name: "GeneratedDocumentId",
                table: "donations");

            migrationBuilder.CreateTable(
                name: "tax_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CerfaType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    PdfPath = table.Column<string>(type: "text", nullable: true),
                    SendError = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentToEmail = table.Column<string>(type: "text", nullable: true),
                    SignatureImagePath = table.Column<string>(type: "text", nullable: true),
                    SnapshotAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    SnapshotContactAddress = table.Column<string>(type: "text", nullable: true),
                    SnapshotContactDisplayName = table.Column<string>(type: "text", nullable: false),
                    SnapshotContactSiret = table.Column<string>(type: "text", nullable: true),
                    SnapshotDonationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SnapshotDonationType = table.Column<string>(type: "text", nullable: false),
                    SnapshotOrgCity = table.Column<string>(type: "text", nullable: true),
                    SnapshotOrgFiscalStatus = table.Column<string>(type: "text", nullable: true),
                    SnapshotOrgName = table.Column<string>(type: "text", nullable: false),
                    SnapshotOrgPostalCode = table.Column<string>(type: "text", nullable: true),
                    SnapshotOrgRna = table.Column<string>(type: "text", nullable: true),
                    SnapshotOrgSiret = table.Column<string>(type: "text", nullable: true),
                    SnapshotOrgStreet = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TaxReductionRate = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tax_receipts_donations_DonationId",
                        column: x => x.DonationId,
                        principalTable: "donations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_receipts_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "email_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentToEmail = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_logs_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_email_logs_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_email_logs_tax_receipts_TaxReceiptId",
                        column: x => x.TaxReceiptId,
                        principalTable: "tax_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_logs_ContactId",
                table: "email_logs",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_email_logs_OrganizationId",
                table: "email_logs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_email_logs_TaxReceiptId",
                table: "email_logs",
                column: "TaxReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_tax_receipts_DonationId",
                table: "tax_receipts",
                column: "DonationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_receipts_OrganizationId_OrderNumber",
                table: "tax_receipts",
                columns: new[] { "OrganizationId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_receipts_OrganizationId_Status",
                table: "tax_receipts",
                columns: new[] { "OrganizationId", "Status" });
        }
    }
}
