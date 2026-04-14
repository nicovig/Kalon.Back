namespace Kalon.Back.Models;

// Donation.cs
// Représente un don, une cotisation ou un soutien reçu par l'association
public class Donation
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; }
    public Guid ContactId { get; set; }
    public Contact Contact { get; set; }

    // montant en euros — 0 si don en nature (valorisation optionnelle)
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }

    // "financial" | "in_kind" | "sponsoring"
    public string DonationType { get; set; }

    // "bank_transfer" | "cash" | "check" | "other"
    public string? PaymentMethod { get; set; }

    // note libre — ex: "Don en mémoire de...", "Versement en deux fois"
    public string? Notes { get; set; }

    // si true : le nom du donateur n'apparaît pas sur le reçu fiscal
    public bool IsAnonymous { get; set; } = false;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // navigation vers le reçu fiscal — null si pas encore généré
    public Guid? GeneratedDocumentId { get; set; }
    public GeneratedDocument? GeneratedDocument { get; set; }
}