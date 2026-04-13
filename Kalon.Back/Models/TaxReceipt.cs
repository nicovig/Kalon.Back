namespace Kalon.Back.Models;

public class TaxReceipt
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; }
    public Guid DonationId { get; set; }
    public Donation Donation { get; set; }

    // ── numérotation ──────────────────────────────
    public string OrderNumber { get; set; }        // "2026-001" — séquentiel par org et par année
    public string CerfaType { get; set; }          // "11580" | "16216"
    public decimal TaxReductionRate { get; set; }  // 0.66 | 0.75 | 0.60

    // ── snapshot légal au moment de la génération ─
    // ces données sont figées à la génération — toute modification de l'asso n'affecte pas les reçus passés
    public string SnapshotOrgName { get; set; }
    public string? SnapshotOrgRna { get; set; }
    public string? SnapshotOrgSiret { get; set; }
    public string? SnapshotOrgFiscalStatus { get; set; }
    public string? SnapshotOrgStreet { get; set; }
    public string? SnapshotOrgPostalCode { get; set; }
    public string? SnapshotOrgCity { get; set; }

    // snapshot donateur
    public string SnapshotContactDisplayName { get; set; }
    public string? SnapshotContactAddress { get; set; }
    public string? SnapshotContactSiret { get; set; }  // si entreprise

    // snapshot donation
    public decimal SnapshotAmount { get; set; }
    public DateTime SnapshotDonationDate { get; set; }
    public string SnapshotDonationType { get; set; }   // "financial" | "in_kind" | "sponsoring"


    // bloc signature/tampon utilisé — snapshot du chemin au moment de la génération
    public string? SignatureImagePath { get; set; }    // copié depuis ContentBlock.StoredPath

    // ── PDF et statut ─────────────────────────────
    public string? PdfPath { get; set; }               // /var/kalon/receipts/{orgId}/{year}/{orderNumber}.pdf
    // "pending" | "generated" | "sent" | "error"
    public string Status { get; set; }
    public DateTime? GeneratedAt { get; set; }

    // ── trace d'envoi ─────────────────────────────
    public string? SentToEmail { get; set; }
    public DateTime? SentAt { get; set; }
    public string? SendError { get; set; }

    public DateTime CreatedAt { get; set; }

}

public static class TaxReceiptStatuses
{
    public const string Pending = "pending";
    public const string Generated = "generated";
    public const string Sent = "sent";
    public const string Error = "error";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Pending,
        Generated,
        Sent,
        Error
    };

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value);
}