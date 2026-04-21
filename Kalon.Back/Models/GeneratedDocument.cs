namespace Kalon.Back.Models;

public class GeneratedDocument
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; }
    public ICollection<Donation> Donations { get; set; } = new List<Donation>();

    // "cerfa_11580"            → reçu fiscal particulier (réduction IR 66% ou 75%)
    // "cerfa_16216"            → reçu fiscal entreprise mécène (réduction IS 60%)
    // "membership_certificate" → attestation de cotisation (membre adhérent)
    // "payment_attestation"    → attestation de paiement (hors périmètre Cerfa)
    public string DocumentType { get; set; }

    // numérotation séquentielle par organisation et par année : "2026-001"
    // null si DocumentType == "membership_certificate" (pas de numérotation Cerfa)
    public string? OrderNumber { get; set; }

    // taux de réduction fiscale affiché sur le document : 0.66 | 0.75 | 0.60
    // null si DocumentType == "membership_certificate"
    public decimal? TaxReductionRate { get; set; }

    // ── snapshot légal au moment de la génération ─────────────────
    public string SnapshotOrgName { get; set; }
    public string? SnapshotOrgRna { get; set; }
    public string? SnapshotOrgSiret { get; set; }
    public string? SnapshotOrgFiscalStatus { get; set; }
    public string? SnapshotOrgStreet { get; set; }
    public string? SnapshotOrgPostalCode { get; set; }
    public string? SnapshotOrgCity { get; set; }

    // snapshot contact
    public string SnapshotContactDisplayName { get; set; }
    public string? SnapshotContactAddress { get; set; }
    public string? SnapshotContactSiret { get; set; }  // si entreprise

    // snapshot donation / cotisation
    public decimal SnapshotAmount { get; set; }
    public DateTime SnapshotDonationDate { get; set; }
    public string SnapshotDonationType { get; set; }

    // ── personnalisation ──────────────────────────────────────────
    public string? SignatureImagePath { get; set; }

    // ── PDF et statut ─────────────────────────────────────────────
    // /var/kalon/documents/{orgId}/{year}/{documentType}/{orderNumber}.pdf
    public string? PdfPath { get; set; }

    // "pending" | "generated" | "sent" | "error"
    public string Status { get; set; }
    public DateTime? GeneratedAt { get; set; }

    // ── trace d'envoi ─────────────────────────────────────────────
    public string? SentToEmail { get; set; }
    public DateTime? SentAt { get; set; }
    public string? SendError { get; set; }

    public DateTime CreatedAt { get; set; }
}

public static class GeneratedDocumentStatuses
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

public static class DocumentType
{
    public const string Cerfa11580 = "cerfa_11580";
    public const string Cerfa16216 = "cerfa_16216";
    public const string PaymentAttestation = "payment_attestation";
    public const string MembershipCertificate = "membership_certificate";
    public const string Message = "message";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Cerfa11580,
        Cerfa16216,
        PaymentAttestation,
        MembershipCertificate,
        Message,
    };

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value);

    public static bool RequiresOrderNumber(string? value) =>
        value is Cerfa11580 or Cerfa16216;

    public static bool IsTaxDeductible(string? value) =>
        value is Cerfa11580 or Cerfa16216;
}