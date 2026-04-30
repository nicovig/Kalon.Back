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
        Token = "{{enterprise_name}}"
    };

    private static readonly IReadOnlyList<MailEditorVariableTag> BaseTags = new[]
    {
        new MailEditorVariableTag { Id = "prenom", Label = "Prénom", Token = "{{prenom}}" },
        new MailEditorVariableTag { Id = "nom", Label = "Nom", Token = "{{nom}}" },
        new MailEditorVariableTag { Id = "totalDonation", Label = "Total des contributions", Token = "{{totalDonation}}" },
        new MailEditorVariableTag { Id = "firstDonationAt", Label = "Date première contribution", Token = "{{firstDonationAt}}" },
        new MailEditorVariableTag { Id = "lastDonation", Label = "Date dernière contribution", Token = "{{lastDonation}}" },
        new MailEditorVariableTag { Id = "averageDonationAmount", Label = "Moyenne des contributions", Token = "{{averageDonationAmount}}" },
        new MailEditorVariableTag { Id = "donationCount", Label = "Nombre de contributions", Token = "{{donationCount}}" }
    };

    public static IReadOnlyList<MailEditorVariableTag> Get(bool hasCompanyRecipient)
    {
        var tags = BaseTags.ToList();
        if (hasCompanyRecipient)
            tags.Insert(2, CompanyTag);
        return tags;
    }
}
