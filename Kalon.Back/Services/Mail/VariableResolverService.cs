using Kalon.Back.Models;
using System.Net;
using System.Text.RegularExpressions;

namespace Kalon.Back.Services.Mail;

public interface IVariableResolverService
{
    string Resolve(string template, Contact contact, Organization org);
    IReadOnlyList<MailEditorVariableTag> GetAvailableTags(bool hasCompanyRecipient);
}


public class VariableResolverService : IVariableResolverService
{
    public IReadOnlyList<MailEditorVariableTag> GetAvailableTags(bool hasCompanyRecipient)
        => MailEditorVariableTagCatalog.Get(hasCompanyRecipient);

    public string Resolve(string template, Contact contact, Organization org)
    {
        if (string.IsNullOrEmpty(template)) return template;

        var normalizedTemplate = WebUtility.HtmlDecode(template)
            .Replace('\u00A0', ' ');

        var resolved = normalizedTemplate
            // ── contact ───────────────────────────────────────────
            .Replace("{{prenom}}", contact.Firstname ?? "")
            .Replace("{{nom}}", contact.Lastname ?? "")
            .Replace("{{prenom_nom}}", $"{contact.Firstname} {contact.Lastname}".Trim())
            .Replace("{{email}}", contact.Email ?? "")
            .Replace("{{telephone}}", contact.Phone ?? "")
            .Replace("{{metier}}", contact.JobTitle ?? "")

            // adresse contact
            .Replace("{{adresse_rue}}", contact.Address?.Street ?? "")
            .Replace("{{adresse_cp}}", contact.Address?.PostalCode ?? "")
            .Replace("{{adresse_ville}}", contact.Address?.City ?? "")
            .Replace("{{adresse_pays}}", contact.Address?.Country ?? "")
            .Replace("{{adresse_complete}}",
                contact.Address != null
                    ? $"{contact.Address.Street}, {contact.Address.PostalCode} {contact.Address.City}"
                    : "")

            // entreprise (si Kind == company)
            .Replace("{{nom_entreprise}}", contact.Enterprise?.Name ?? "")
            .Replace("{{enterprise_name}}", contact.Enterprise?.Name ?? "")
            .Replace("{{siret_entreprise}}", contact.Enterprise?.Siret ?? "")

            // ── dons (calculés — [NotMapped]) ─────────────────────
            .Replace("{{total_dons}}",
                contact.TotalDonation.ToString("C", new System.Globalization.CultureInfo("fr-FR")))
            .Replace("{{totalDonation}}",
                contact.TotalDonation.ToString("C", new System.Globalization.CultureInfo("fr-FR")))
            .Replace("{{nombre_dons}}", contact.DonationCount.ToString())
            .Replace("{{donationCount}}", contact.DonationCount.ToString())
            .Replace("{{firstDonationAt}}",
                contact.FirstDonationAt?.ToString("dd/MM/yyyy") ?? "jamais")
            .Replace("{{date_dernier_don}}",
                contact.LastDonation?.ToString("dd/MM/yyyy") ?? "jamais")
            .Replace("{{lastDonation}}",
                contact.LastDonation?.ToString("dd/MM/yyyy") ?? "jamais")
            .Replace("{{averageDonationAmount}}",
                contact.AverageDonationAmount.ToString("C", new System.Globalization.CultureInfo("fr-FR")))
            .Replace("{{mois_depuis_dernier_don}}",
                contact.LastDonation.HasValue
                    ? CalculateMonthsSince(contact.LastDonation.Value).ToString()
                    : "")

            // ── organisation ──────────────────────────────────────
            .Replace("{{nom_association}}", org.Name)
            .Replace("{{association}}", org.Name)
            .Replace("{{rna}}", org.RNA ?? "")
            .Replace("{{siret_association}}", org.SIRET ?? "")
            .Replace("{{adresse_association}}",
                org.Street != null
                    ? $"{org.Street}, {org.PostalCode} {org.City}"
                    : "")
            .Replace("{{email_association}}", org.Email ?? "")
            .Replace("{{telephone_association}}", org.Phone ?? "")
            .Replace("{{site_association}}", org.Website ?? "")
            .Replace("{{lien_paiement}}", org.Website ?? "")
            .Replace("{{payment_link}}", org.Website ?? "")

            // ── dates utiles ──────────────────────────────────────
            .Replace("{{date_du_jour}}", DateTime.Now.ToString("dd/MM/yyyy"))
            .Replace("{{annee_en_cours}}", DateTime.Now.Year.ToString())
            .Replace("{{mois_en_cours}}",
                DateTime.Now.ToString("MMMM yyyy",
                    new System.Globalization.CultureInfo("fr-FR")));

        return Regex.Replace(resolved, @"\{\{[^{}]+\}\}", "");
    }

    private static int CalculateMonthsSince(DateTime date)
    {
        var now = DateTime.UtcNow;
        return (now.Year - date.Year) * 12 + now.Month - date.Month;
    }
}