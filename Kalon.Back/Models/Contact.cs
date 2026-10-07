using System.ComponentModel.DataAnnotations.Schema;

namespace Kalon.Back.Models;

// Contact.cs
// Représente toute personne physique ou morale en relation avec l'association
// 5 types : donor, company, member, helper, out (décédé)
public class Contact
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; }

    public bool IsEnterprise { get; set; }

    // true si le contact est décédé - exclu de tous les envois et calculs
    // les autres statuts (new, to_remind, inactive, active) sont calculés dynamiquement
    public bool IsOut { get; set; }

    // ── identité ──────────────────────────────────────────
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public DateTime? BirthDate { get; set; }

    // "male" | "female" | "other"
    public string? Gender { get; set; }

    // note libre sur le contact - ex: "Appeler avant de relancer"
    public string? Notes { get; set; }

    // département calculé depuis PostalCode - stocké pour la cartographie
    // ex: "44", "75", "971" (DOM)
    public string? Department { get; set; }

    // préférence d'envoi des reçus fiscaux - surcharge le défaut de l'organisation
    // "instantly" | "monthly" | "quarterly" | "semesterly" | "yearly"
    public string? PreferredFrequencySendingReceipt { get; set; }

    public DateTime? LastMembershipDate { get; set; }

    public DateTime? MembershipEndDate { get; set; }

    public bool IsFamilyMembership { get; set; }

    // ── adresse (owned entity - stockée dans la table contacts) ───
    public ContactAddress? Address { get; set; }

    // ── données entreprise (owned entity - stockée dans la table contacts) ─
    // renseigné uniquement si Kind == "company"
    public ContactEnterprise? Enterprise { get; set; }

    // ── métadonnées ───────────────────────────────────────
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ── champs calculés - non mappés en base ──────────────
    // calculés via requête SQL agrégée dans le service
    [NotMapped]
    public decimal TotalDonation { get; set; }

    [NotMapped]
    public DateTime? FirstDonationAt { get; set; }

    [NotMapped]
    public decimal? FirstDonationAmount { get; set; }

    [NotMapped]
    public DateTime? LastDonation { get; set; }

    [NotMapped]
    public decimal? LastDonationAmount { get; set; }

    [NotMapped]
    public decimal AverageDonationAmount { get; set; }

    [NotMapped]
    public int DonationCount { get; set; }

    // ── navigations ───────────────────────────────────────
    public ICollection<Donation> Donations { get; set; } = new List<Donation>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}

// ContactAddress.cs
// Owned entity - pas de table séparée, colonnes préfixées dans "contacts"
// ex: Address_Street, Address_PostalCode...
public class ContactAddress
{
    public required string Street { get; set; }
    public string PostalCode { get; set; }
    public string City { get; set; }
    public string Country { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

// ContactEnterprise.cs
// Owned entity - pas de table séparée, colonnes préfixées dans "contacts"
// renseigné uniquement si Contact.Kind == "company"
public class ContactEnterprise
{
    public string Name { get; set; }
    public string? Siret { get; set; }

    // type de relation avec l'association
    // "mecenat"   → don sans contrepartie - Cerfa 16216 possible
    // "sponsoring" → contrepartie publicitaire - pas de Cerfa mécénat
    // "donation"  → don simple hors cadre fiscal
    public string? SupportKind { get; set; }

    // adresse du siège de l'entreprise
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    // contact référent dans l'entreprise
    public string? ContactFirstname { get; set; }
    public string? ContactLastname { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
}