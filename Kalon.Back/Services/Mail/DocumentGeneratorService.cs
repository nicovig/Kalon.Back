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
                        Cerfa11580DocumentLayout.Render(col, data);
                        break;

                    case Models.DocumentType.Cerfa16216:
                        Cerfa16216DocumentLayout.Render(col, data);
                        break;

                    case Models.DocumentType.MembershipCertificate:
                        MembershipCertificateDocumentLayout.Render(col, data);
                        break;

                    case Models.DocumentType.PaymentAttestation:
                        PaymentAttestationDocumentLayout.Render(col, data);
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

        if (!string.IsNullOrWhiteSpace(data.ResolvedSubject))
        {
            col.Item().Text(t =>
            {
                t.Span("Objet : ").Bold().Underline().FontSize(12);
                t.Span(data.ResolvedSubject).Bold().Underline().FontSize(12);
            });
            col.Item().PaddingVertical(12);
        }

        // corps du mail — HTML simplifié
        col.Item().Text(StripHtml(data.ResolvedHtml)).FontSize(11);

        col.Item().PaddingVertical(24);

        // signature
        BuildSignature(col, data, false);
    }

    private void BuildSignature(ColumnDescriptor col, PrintPageData data, bool includeDate = true)
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

            if (includeDate)
            {
                sig.Item().Text(DateTime.Now.ToString("dd MMMM yyyy",
                    new System.Globalization.CultureInfo("fr-FR")))
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static string DisplayName(Contact contact) =>
        contact.Kind == ContactKinds.Company
            && contact.Enterprise?.Name is not null
            ? contact.Enterprise.Name
            : $"{contact.Firstname} {contact.Lastname}".Trim();

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