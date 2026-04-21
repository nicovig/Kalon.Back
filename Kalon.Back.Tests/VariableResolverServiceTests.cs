using Kalon.Back.Models;
using Kalon.Back.Services.Mail;

namespace Kalon.Back.Tests;

public class VariableResolverServiceTests
{
    private readonly IVariableResolverService _resolver = new VariableResolverService();

    private static Contact MakeContact(Action<Contact>? configure = null)
    {
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            Kind = ContactKinds.Donor,
            Firstname = "Marie",
            Lastname = "Dupont",
            Email = "marie.dupont@example.com",
            Phone = "06 01 02 03 04",
            TotalDonation = 320m,
            DonationCount = 3,
            LastDonation = DateTime.UtcNow.AddMonths(-14),
            Address = new ContactAddress
            {
                Street = "12 rue des Lilas",
                PostalCode = "49000",
                City = "Angers",
                Country = "France"
            }
        };
        configure?.Invoke(contact);
        return contact;
    }

    private static Organization MakeOrg() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Magnificat",
        RNA = "W441234567",
        SIRET = "12345678900014",
        Email = "dg@magnificat.asso.fr",
        Street = "8 rue de la Paix",
        PostalCode = "49100",
        City = "Angers"
    };

    [Fact]
    public void Resolve_BasicContactVariables_ReplacesCorrectly()
    {
        var template = "Bonjour {{prenom}} {{nom}},";
        var result = _resolver.Resolve(template, MakeContact(), MakeOrg());
        Assert.Equal("Bonjour Marie Dupont,", result);
    }

    [Fact]
    public void Resolve_OrgVariables_ReplacesCorrectly()
    {
        var template = "De la part de {{nom_association}} — {{email_association}}";
        var result = _resolver.Resolve(template, MakeContact(), MakeOrg());
        Assert.Equal("De la part de Magnificat — dg@magnificat.asso.fr", result);
    }

    [Fact]
    public void Resolve_TotalDons_FormatsAsCurrency()
    {
        var template = "Vous avez donné {{total_dons}} au total.";
        var result = _resolver.Resolve(template, MakeContact(), MakeOrg());
        Assert.Contains("320", result);
        Assert.Contains("€", result);
    }

    [Fact]
    public void Resolve_MoisDepuisDernigerDon_CalculatesCorrectly()
    {
        var contact = MakeContact(c => c.LastDonation = DateTime.UtcNow.AddMonths(-6));
        var template = "Cela fait {{mois_depuis_dernier_don}} mois.";
        var result = _resolver.Resolve(template, contact, MakeOrg());
        Assert.Contains("6", result);
    }

    [Fact]
    public void Resolve_NullOptionalFields_ReplacesWithEmpty()
    {
        var contact = MakeContact(c =>
        {
            c.Phone = null;
            c.JobTitle = null;
            c.LastDonation = null;
        });
        var template = "Tel: {{telephone}} — Métier: {{metier}} — Dernier don: {{date_dernier_don}}";
        var result = _resolver.Resolve(template, contact, MakeOrg());
        Assert.Equal("Tel:  — Métier:  — Dernier don: jamais", result);
    }

    [Fact]
    public void Resolve_EnterpriseContact_ReplacesEnterpriseVariables()
    {
        var contact = MakeContact(c =>
        {
            c.Kind = ContactKinds.Company;
            c.Enterprise = new ContactEnterprise
            {
                Name = "Alpha SAS",
                Siret = "98765432100017"
            };
        });
        var template = "Entreprise : {{nom_entreprise}} (SIRET : {{siret_entreprise}})";
        var result = _resolver.Resolve(template, contact, MakeOrg());
        Assert.Equal("Entreprise : Alpha SAS (SIRET : 98765432100017)", result);
    }

    [Fact]
    public void Resolve_EmptyTemplate_ReturnsEmpty()
    {
        var result = _resolver.Resolve("", MakeContact(), MakeOrg());
        Assert.Equal("", result);
    }

    [Fact]
    public void Resolve_NoVariables_ReturnsTemplateUnchanged()
    {
        var template = "Bonjour, merci pour votre soutien.";
        var result = _resolver.Resolve(template, MakeContact(), MakeOrg());
        Assert.Equal(template, result);
    }

    [Fact]
    public void Resolve_AdresseComplete_FormatsCorrectly()
    {
        var template = "{{adresse_complete}}";
        var result = _resolver.Resolve(template, MakeContact(), MakeOrg());
        Assert.Equal("12 rue des Lilas, 49000 Angers", result);
    }

    [Fact]
    public void Resolve_AdresseCompleteNull_ReturnsEmpty()
    {
        var contact = MakeContact(c => c.Address = null);
        var result = _resolver.Resolve("{{adresse_complete}}", contact, MakeOrg());
        Assert.Equal("", result);
    }
}
