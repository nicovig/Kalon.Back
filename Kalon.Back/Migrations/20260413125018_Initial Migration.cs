using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalon.Back.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeranId = table.Column<Guid>(type: "uuid", nullable: false),
                    Firstname = table.Column<string>(type: "text", nullable: false),
                    Lastname = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AssociationName = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Salt = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Street = table.Column<string>(type: "text", nullable: true),
                    PostalCode = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RNA = table.Column<string>(type: "text", nullable: false),
                    SIRET = table.Column<string>(type: "text", nullable: false),
                    FiscalStatus = table.Column<string>(type: "text", nullable: false),
                    DefaultReceiptFrequency = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organizations_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Firstname = table.Column<string>(type: "text", nullable: false),
                    Lastname = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    JobTitle = table.Column<string>(type: "text", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Gender = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Department = table.Column<string>(type: "text", nullable: true),
                    PreferredFrequencySendingReceipt = table.Column<string>(type: "text", nullable: true),
                    address_street = table.Column<string>(type: "text", nullable: true),
                    address_postal_code = table.Column<string>(type: "text", nullable: true),
                    address_city = table.Column<string>(type: "text", nullable: true),
                    address_country = table.Column<string>(type: "text", nullable: true),
                    address_phone = table.Column<string>(type: "text", nullable: true),
                    address_email = table.Column<string>(type: "text", nullable: true),
                    enterprise_name = table.Column<string>(type: "text", nullable: true),
                    enterprise_siret = table.Column<string>(type: "text", nullable: true),
                    enterprise_support_kind = table.Column<string>(type: "text", nullable: true),
                    enterprise_street = table.Column<string>(type: "text", nullable: true),
                    enterprise_postal_code = table.Column<string>(type: "text", nullable: true),
                    enterprise_city = table.Column<string>(type: "text", nullable: true),
                    enterprise_country = table.Column<string>(type: "text", nullable: true),
                    enterprise_contact_firstname = table.Column<string>(type: "text", nullable: true),
                    enterprise_contact_lastname = table.Column<string>(type: "text", nullable: true),
                    enterprise_contact_email = table.Column<string>(type: "text", nullable: true),
                    enterprise_contact_phone = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contacts_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "content_blocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    StoredPath = table.Column<string>(type: "text", nullable: true),
                    MimeType = table.Column<string>(type: "text", nullable: true),
                    UsableInEmail = table.Column<bool>(type: "boolean", nullable: false),
                    UsableInReceipt = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_blocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_blocks_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    EmailTemplateType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_templates_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_logos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    StoredPath = table.Column<string>(type: "text", nullable: false),
                    MimeType = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_logos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_logos_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "donations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DonationType = table.Column<string>(type: "text", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_donations_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_donations_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DonationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    CerfaType = table.Column<string>(type: "text", nullable: false),
                    TaxReductionRate = table.Column<decimal>(type: "numeric", nullable: false),
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
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentToEmail = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "IX_contacts_OrganizationId",
                table: "contacts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_OrganizationId_Department",
                table: "contacts",
                columns: new[] { "OrganizationId", "Department" });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_OrganizationId_Kind",
                table: "contacts",
                columns: new[] { "OrganizationId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_OrganizationId_Status",
                table: "contacts",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_content_blocks_OrganizationId_Kind",
                table: "content_blocks",
                columns: new[] { "OrganizationId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_donations_ContactId",
                table: "donations",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_donations_OrganizationId",
                table: "donations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_donations_OrganizationId_Date",
                table: "donations",
                columns: new[] { "OrganizationId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_donations_OrganizationId_DonationType",
                table: "donations",
                columns: new[] { "OrganizationId", "DonationType" });

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
                name: "IX_email_templates_OrganizationId",
                table: "email_templates",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_organization_logos_OrganizationId",
                table: "organization_logos",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizations_UserId",
                table: "organizations",
                column: "UserId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_MeranId",
                table: "users",
                column: "MeranId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_blocks");

            migrationBuilder.DropTable(
                name: "email_logs");

            migrationBuilder.DropTable(
                name: "email_templates");

            migrationBuilder.DropTable(
                name: "organization_logos");

            migrationBuilder.DropTable(
                name: "tax_receipts");

            migrationBuilder.DropTable(
                name: "donations");

            migrationBuilder.DropTable(
                name: "contacts");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
