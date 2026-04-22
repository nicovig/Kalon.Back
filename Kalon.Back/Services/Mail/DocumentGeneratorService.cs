using Kalon.Back.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Net;
using System.Text.RegularExpressions;

namespace Kalon.Back.Services.Mail;

public interface IDocumentGeneratorService
{
    // génère un PDF multi-pages (1 page par destinataire)
    byte[] GenerateMultiPage(List<PrintPageData> pages);

    // génère un PDF pour un seul document (aperçu)
    byte[] GenerateSingle(PrintPageData page);
}


public class DocumentGeneratorService : IDocumentGeneratorService
{
    public byte[] GenerateMultiPage(List<PrintPageData> pages)
    {
        return Document.Create(container =>
        {
            foreach (var page in pages)
                AddPage(container, page);
        }).GeneratePdf();
    }

    public byte[] GenerateSingle(PrintPageData page)
    {
        return Document.Create(container =>
        {
            AddPage(container, page);
        }).GeneratePdf();
    }

    private void AddPage(IDocumentContainer container, PrintPageData data)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(11));

            page.Content().Column(col =>
            {
                // dispatch selon le type de document
                switch (data.DocumentType)
                {
                    case Models.DocumentType.Cerfa11580:
                        BuildCerfa11580(col, data);
                        break;

                    case Models.DocumentType.Cerfa16216:
                        BuildCerfa16216(col, data);
                        break;

                    case Models.DocumentType.MembershipCertificate:
                        BuildMembershipCertificate(col, data);
                        break;

                    case Models.DocumentType.PaymentAttestation:
                        BuildPaymentAttestation(col, data);
                        break;

                    default:
                        // relance / courrier simple
                        BuildSimpleLetter(col, data);
                        break;
                }
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Document généré par Kalon — kalon-app.fr")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });
    }

    // ── Courrier simple (relance, message libre) ───────────────────

    private void BuildSimpleLetter(ColumnDescriptor col, PrintPageData data)
    {
        var org = data.Organization;
        var contact = data.Contact;

        // en-tête expéditeur (asso) — haut gauche
        col.Item().Text(t =>
        {
            t.Line(org.Name).Bold().FontSize(12);
            if (org.Street != null)
                t.Line($"{org.Street}");
            if (org.PostalCode != null)
                t.Line($"{org.PostalCode} {org.City}");
            if (org.Email != null)
                t.Line(org.Email).FontColor(Colors.Grey.Darken1);
        });

        col.Item().PaddingVertical(20);

        // adresse destinataire — haut droite (fenêtre enveloppe)
        col.Item().AlignRight().Text(t =>
        {
            var name = DisplayName(contact);
            t.Line(name).Bold();
            if (contact.Address != null)
            {
                t.Line(contact.Address.Street ?? "");
                t.Line($"{contact.Address.PostalCode} {contact.Address.City}");
                if (contact.Address.Country != null
                    && contact.Address.Country.ToLower() != "france")
                    t.Line(contact.Address.Country.ToUpper());
            }
        });

        col.Item().PaddingVertical(20);

        // date
        col.Item().AlignRight().Text(
            $"Le {DateTime.Now:dd MMMM yyyy}".ToLower())
            .FontColor(Colors.Grey.Darken1);

        col.Item().PaddingVertical(16);

        // corps du mail — HTML simplifié
        col.Item().Text(StripHtml(data.ResolvedHtml)).FontSize(11);

        col.Item().PaddingVertical(24);

        // signature
        BuildSignature(col, data);
    }

    // ── Cerfa 11580 (particulier) ──────────────────────────────────

    private void BuildCerfa11580(ColumnDescriptor col, PrintPageData data)
    {
        var doc = data.GeneratedDocument!;
        var org = data.Organization;

        // titre
        col.Item().Background(Colors.Grey.Lighten3).Padding(8)
            .AlignCenter()
            .Text(t =>
            {
                t.Line("REÇU AU TITRE DES DONS À DES ORGANISMES D'INTÉRÊT GÉNÉRAL")
                    .Bold().FontSize(10);
                t.Line("CERFA N° 11580*05").FontSize(9)
                    .FontColor(Colors.Grey.Darken2);
            });

        col.Item().PaddingVertical(10);

        // bloc organisme bénéficiaire
        BuildLegalBlock(col, "L'organisme bénéficiaire", items =>
        {
            items.Add(("Nom", doc.SnapshotOrgName));
            items.Add(("Adresse", FormatAddress(
                doc.SnapshotOrgStreet,
                doc.SnapshotOrgPostalCode,
                doc.SnapshotOrgCity)));
            if (doc.SnapshotOrgRna != null)
                items.Add(("N° RNA", doc.SnapshotOrgRna));
            if (doc.SnapshotOrgSiret != null)
                items.Add(("SIRET", doc.SnapshotOrgSiret));
        });

        col.Item().PaddingVertical(8);

        // bloc donateur
        BuildLegalBlock(col, "Le donateur", items =>
        {
            items.Add(("Nom", doc.SnapshotContactDisplayName));
            if (doc.SnapshotContactAddress != null)
                items.Add(("Adresse", doc.SnapshotContactAddress));
        });

        col.Item().PaddingVertical(8);

        // bloc don
        BuildLegalBlock(col, "Nature et montant du don", items =>
        {
            items.Add(("Montant", doc.SnapshotAmount.ToString("C",
                new System.Globalization.CultureInfo("fr-FR"))));
            items.Add(("Date", doc.SnapshotDonationDate.ToString("dd/MM/yyyy")));
            items.Add(("Nature", TranslateDonationType(doc.SnapshotDonationType)));
        });

        col.Item().PaddingVertical(8);

        // mention légale fiscale — NON MODIFIABLE
        var rate = (int)((doc.TaxReductionRate ?? 0.66m) * 100);
        col.Item().Background(Colors.Yellow.Lighten4).Padding(6).Text(
            $"Ce reçu ouvre droit à une réduction d'impôt de {rate}% du montant " +
            $"du don dans la limite de 20% du revenu imposable (article 200 du CGI).")
            .FontSize(9).Italic();

        col.Item().PaddingVertical(8);

        // encart message personnalisé (optionnel)
        if (!string.IsNullOrEmpty(data.ResolvedHtml))
        {
            col.Item().BorderLeft(2)
                      .BorderColor(Colors.Grey.Lighten1)
                .PaddingLeft(8)
                .Text(StripHtml(data.ResolvedHtml))
                .FontSize(10).FontColor(Colors.Grey.Darken2);

            col.Item().PaddingVertical(8);
        }

        // numéro d'ordre
        col.Item().AlignRight().Text(
            $"N° d'ordre : {doc.OrderNumber}")
            .FontSize(9).FontColor(Colors.Grey.Medium);

        col.Item().PaddingVertical(16);

        // signature
        BuildSignature(col, data);
    }

    // ── Cerfa 16216 (entreprise mécène) ───────────────────────────

    private void BuildCerfa16216(ColumnDescriptor col, PrintPageData data)
    {
        var doc = data.GeneratedDocument!;

        // titre
        col.Item().Background(Colors.Grey.Lighten3).Padding(8)
            .AlignCenter()
            .Text(t =>
                {
                    t.Line("REÇU DE DON — MÉCÉNAT D'ENTREPRISE")
                        .Bold().FontSize(10);
                    t.Line("CERFA N° 16216*02 — Article 238 bis du CGI")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });

        col.Item().PaddingVertical(10);

        BuildLegalBlock(col, "L'organisme bénéficiaire", items =>
        {
            items.Add(("Nom", doc.SnapshotOrgName));
            items.Add(("Adresse", FormatAddress(
                doc.SnapshotOrgStreet,
                doc.SnapshotOrgPostalCode,
                doc.SnapshotOrgCity)));
            if (doc.SnapshotOrgRna != null)
                items.Add(("N° RNA", doc.SnapshotOrgRna));
            if (doc.SnapshotOrgSiret != null)
                items.Add(("SIRET", doc.SnapshotOrgSiret));
        });

        col.Item().PaddingVertical(8);

        BuildLegalBlock(col, "L'entreprise versante", items =>
        {
            items.Add(("Raison sociale", doc.SnapshotContactDisplayName));
            if (doc.SnapshotContactSiret != null)
                items.Add(("SIRET", doc.SnapshotContactSiret));
            if (doc.SnapshotContactAddress != null)
                items.Add(("Adresse", doc.SnapshotContactAddress));
        });

        col.Item().PaddingVertical(8);

        BuildLegalBlock(col, "Nature et montant du versement", items =>
        {
            items.Add(("Montant", doc.SnapshotAmount.ToString("C",
                new System.Globalization.CultureInfo("fr-FR"))));
            items.Add(("Date", doc.SnapshotDonationDate.ToString("dd/MM/yyyy")));
            items.Add(("Nature", TranslateDonationType(doc.SnapshotDonationType)));
        });

        col.Item().PaddingVertical(8);

        // mention légale — NON MODIFIABLE
        col.Item().Background(Colors.Yellow.Lighten4).Padding(6).Text(
            "Ce versement ouvre droit à une réduction d'impôt sur les sociétés " +
            "de 60% du montant, plafonnée à 20 000 € ou 5‰ du CA HT " +
            "(article 238 bis du CGI).")
            .FontSize(9).Italic();

        col.Item().PaddingVertical(8);

        if (!string.IsNullOrEmpty(data.ResolvedHtml))
        {
            col.Item().BorderLeft(2)
                      .BorderColor(Colors.Grey.Lighten1)
                .PaddingLeft(8)
                .Text(StripHtml(data.ResolvedHtml))
                .FontSize(10).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingVertical(8);
        }

        col.Item().AlignRight().Text($"N° d'ordre : {doc.OrderNumber}")
            .FontSize(9).FontColor(Colors.Grey.Medium);

        col.Item().PaddingVertical(16);
        BuildSignature(col, data);
    }

    // ── Attestation cotisation ─────────────────────────────────────

    private void BuildMembershipCertificate(ColumnDescriptor col, PrintPageData data)
    {
        var doc = data.GeneratedDocument!;
        var org = data.Organization;

        col.Item().Text("ATTESTATION DE COTISATION")
            .Bold().FontSize(14).AlignCenter();

        col.Item().PaddingVertical(10);

        col.Item().Text(t =>
        {
            t.Line($"L'association {doc.SnapshotOrgName}").Bold();
            t.Line(FormatAddress(
                doc.SnapshotOrgStreet,
                doc.SnapshotOrgPostalCode,
                doc.SnapshotOrgCity));
            if (doc.SnapshotOrgRna != null)
                t.Line($"RNA : {doc.SnapshotOrgRna}");
        });

        col.Item().PaddingVertical(10);

        col.Item().Text(t =>
        {
            t.Span("atteste que ").FontSize(11);
            t.Span(doc.SnapshotContactDisplayName).Bold().FontSize(11);
            t.Span(" est membre adhérent et a versé une cotisation de ").FontSize(11);
            t.Span(doc.SnapshotAmount.ToString("C",
                new System.Globalization.CultureInfo("fr-FR")))
                .Bold().FontSize(11);
            t.Span($" en date du {doc.SnapshotDonationDate:dd/MM/yyyy}.").FontSize(11);
        });

        col.Item().PaddingVertical(8);

        if (!string.IsNullOrEmpty(data.ResolvedHtml))
        {
            col.Item().Text(StripHtml(data.ResolvedHtml)).FontSize(10);
            col.Item().PaddingVertical(8);
        }

        col.Item().AlignRight().Text(
            $"Fait le {DateTime.Now:dd/MM/yyyy}")
            .FontColor(Colors.Grey.Darken1);

        col.Item().PaddingVertical(16);
        BuildSignature(col, data);
    }

    // ── Attestation paiement ───────────────────────────────────────

    private void BuildPaymentAttestation(ColumnDescriptor col, PrintPageData data)
    {
        var doc = data.GeneratedDocument!;

        col.Item().Text("ATTESTATION DE PAIEMENT")
            .Bold().FontSize(14).AlignCenter();

        col.Item().PaddingVertical(10);

        col.Item().Text(t =>
        {
            t.Line($"L'association {doc.SnapshotOrgName}").Bold();
        });

        col.Item().PaddingVertical(10);

        col.Item().Text(t =>
        {
            t.Span("atteste avoir reçu de ").FontSize(11);
            t.Span(doc.SnapshotContactDisplayName).Bold().FontSize(11);
            t.Span(" la somme de ").FontSize(11);
            t.Span(doc.SnapshotAmount.ToString("C",
                new System.Globalization.CultureInfo("fr-FR")))
                .Bold().FontSize(11);
            t.Span($" le {doc.SnapshotDonationDate:dd/MM/yyyy}.").FontSize(11);
        });

        col.Item().PaddingVertical(8);

        if (!string.IsNullOrEmpty(data.ResolvedHtml))
        {
            col.Item().Text(StripHtml(data.ResolvedHtml)).FontSize(10);
            col.Item().PaddingVertical(8);
        }

        col.Item().AlignRight().Text(
            $"Fait le {DateTime.Now:dd/MM/yyyy}")
            .FontColor(Colors.Grey.Darken1);

        col.Item().PaddingVertical(16);
        BuildSignature(col, data);
    }

    // ── Helpers communs ───────────────────────────────────────────

    private void BuildLegalBlock(
        ColumnDescriptor col,
        string title,
        Action<List<(string Label, string Value)>> populate)
    {
        var items = new List<(string Label, string Value)>();
        populate(items);

        col.Item().Border(1, Colors.Grey.Lighten1).Column(block =>
        {
            block.Item().Background(Colors.Grey.Lighten3)
                .Padding(4)
                .Text(title).Bold().FontSize(9);

            foreach (var (label, value) in items)
            {
                block.Item().Row(row =>
                {
                    row.ConstantItem(120).Padding(4)
                        .Text(label + " :").FontSize(9)
                        .FontColor(Colors.Grey.Darken2);
                    row.RelativeItem().Padding(4)
                        .Text(value ?? "").FontSize(9);
                });
            }
        });
    }

    private void BuildSignature(ColumnDescriptor col, PrintPageData data)
    {
        col.Item().AlignRight().Column(sig =>
        {
            // image de signature si disponible
            if (data.SignatureBlock?.StoredPath != null
                && File.Exists(data.SignatureBlock.StoredPath))
            {
                sig.Item().AlignRight()
                    .Image(data.SignatureBlock.StoredPath)
                    .FitWidth()
                    .WithCompressionQuality(ImageCompressionQuality.High);
                sig.Item().PaddingVertical(4);
            }

            // set de la date
            sig.Item().Text(DateTime.Now.ToString("dd MMMM yyyy",
                new System.Globalization.CultureInfo("fr-FR")))
                .FontSize(9).FontColor(Colors.Grey.Darken1);
        });
    }

    private static string FormatAddress(
        string? street, string? postalCode, string? city)
    {
        var parts = new[] { street, $"{postalCode} {city}".Trim() }
            .Where(p => !string.IsNullOrEmpty(p));
        return string.Join(", ", parts);
    }

    private static string DisplayName(Contact contact) =>
        contact.Kind == ContactKinds.Company
            && contact.Enterprise?.Name is not null
            ? contact.Enterprise.Name
            : $"{contact.Firstname} {contact.Lastname}".Trim();

    private static string TranslateDonationType(string type) => type switch
    {
        "financial" => "Don financier",
        "in_kind" => "Don en nature",
        "sponsoring" => "Sponsoring",
        _ => type
    };

    // conversion HTML → texte brut pour les PDFs
    // pour une vraie conversion HTML→PDF plus tard : HtmlRenderer ou wkhtmltopdf
    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var decoded = WebUtility.HtmlDecode(html)
            .Replace('\u00A0', ' ');

        var withLineBreaks = Regex.Replace(decoded, @"<(br|BR)\s*/?>", "\n");
        withLineBreaks = Regex.Replace(withLineBreaks, @"</(p|div|li|h[1-6])>", "\n", RegexOptions.IgnoreCase);

        var withoutTags = Regex.Replace(withLineBreaks, "<.*?>", " ");
        var normalizedLines = Regex.Replace(withoutTags, @"[ \t]{2,}", " ");
        var normalizedBreaks = Regex.Replace(normalizedLines, @"\n{3,}", "\n\n");
        return normalizedBreaks.Trim();
    }
}