namespace Kalon.Back.Models;

// Paramètres de calcul automatique du statut des contacts
// Stockés par organisation — chaque asso définit ses propres règles

public class ContactStatusSettings
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; }

    // "Nouveau" pendant X jours après la création du contact
    public int NewDurationDays { get; set; } = 30;

    // "À relancer" après X mois sans donation
    public int ToRemindAfterMonths { get; set; } = 12;

    // "Inactif" après X mois sans donation
    public int InactiveAfterMonths { get; set; } = 24;

    // "Actif" = aucune des règles ci-dessus ne s'applique (calculé, pas stocké)

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}