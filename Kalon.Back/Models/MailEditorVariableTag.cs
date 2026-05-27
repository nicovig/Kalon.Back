namespace Kalon.Back.Models;

public class MailEditorVariableTag
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public static class MailEditorVariableTagCatalog
{
    private static readonly MailEditorVariableTag CompanyTag = new()
    {
        Id = "enterprise_name",
        Label = "Nom de l'entreprise",
        Token = "{{nom_entreprise}}"
    };

    private static readonly IReadOnlyList<MailEditorVariableTag> BaseTags = new[]
    {
        new MailEditorVariableTag { Id = "prenom", Label = "Prénom", Token = "{{prenom}}" },
        new MailEditorVariableTag { Id = "nom", Label = "Nom", Token = "{{nom}}" },
        new MailEditorVariableTag { Id = "totalContributions", Label = "Total des contributions", Token = "{{totalContributions}}" },
        new MailEditorVariableTag { Id = "premiereContributionLe", Label = "Date première contribution", Token = "{{premiereContributionLe}}" },
        new MailEditorVariableTag { Id = "derniereContributionLe", Label = "Date dernière contribution", Token = "{{derniereContributionLe}}" },
        new MailEditorVariableTag { Id = "montantPremiereDonation", Label = "Montant première contribution", Token = "{{montantPremiereDonation}}" },
        new MailEditorVariableTag { Id = "montantDerniereDonation", Label = "Montant dernière contribution", Token = "{{montantDerniereDonation}}" },
        new MailEditorVariableTag { Id = "contributionMoyenne", Label = "Moyenne des contributions", Token = "{{contributionMoyenne}}" },
        new MailEditorVariableTag { Id = "montantDonations", Label = "Montant total des contributions", Token = "{{montantDonations}}" }
    };

    public static IReadOnlyList<MailEditorVariableTag> Get(bool hasCompanyRecipient)
    {
        var tags = BaseTags.ToList();
        if (hasCompanyRecipient)
            tags.Insert(2, CompanyTag);
        return tags;
    }
}
