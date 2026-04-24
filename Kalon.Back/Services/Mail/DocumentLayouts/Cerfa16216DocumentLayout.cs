using Kalon.Back.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Net;
using System.Text.RegularExpressions;

namespace Kalon.Back.Services.Mail;

internal static class Cerfa16216DocumentLayout
{
    public static void Render(ColumnDescriptor col, PrintPageData data)
    {
        var doc = data.GeneratedDocument!;
        var amount = doc.SnapshotAmount.ToString("C", new System.Globalization.CultureInfo("fr-FR"));
        var giftText = StripHtml(data.ResolvedHtml);

        col.Item().Border(1).BorderColor("#BFDBFE").Background("#EFF6FF").Padding(14).Column(header =>
        {
            header.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("Reçu de mécénat d'entreprise").Bold().FontSize(16).FontColor("#1D4ED8");
                    left.Item().Text("CERFA n°16216*02 - Article 238 bis du CGI").FontSize(10).FontColor("#1E40AF");
                });
                row.ConstantItem(180).AlignRight().Column(right =>
                {
                    right.Item().Text($"N° {doc.OrderNumber ?? "N/A"}").SemiBold().FontSize(10).FontColor("#1E3A8A");
                    right.Item().Text($"Émis le {DateTime.Now:dd/MM/yyyy}").FontSize(9).FontColor("#6B7280");
                });
            });
        });

        col.Item().PaddingTop(10).Border(1).BorderColor("#E5E7EB").Padding(12).Column(sum =>
        {
            sum.Item().Text("Attestation de versement entreprise").Bold().FontSize(13).FontColor("#111827");
            sum.Item().PaddingTop(4).Text(t =>
            {
                t.Span("Nous certifions avoir reçu de ").FontSize(10);
                t.Span(doc.SnapshotContactDisplayName).SemiBold().FontSize(10);
                t.Span(" un versement de ").FontSize(10);
                t.Span(amount).SemiBold().FontSize(10);
                t.Span($" en date du {doc.SnapshotDonationDate:dd/MM/yyyy}.").FontSize(10);
            });
        });

        col.Item().PaddingTop(10).Row(row =>
        {
            row.RelativeItem().Border(1).BorderColor("#E5E7EB").Padding(10).Column(left =>
            {
                left.Item().Text("Organisme bénéficiaire").SemiBold().FontSize(10).FontColor("#111827");
                left.Item().PaddingTop(4).Text(doc.SnapshotOrgName).FontSize(10);
                left.Item().Text(FormatAddress(doc.SnapshotOrgStreet, doc.SnapshotOrgPostalCode, doc.SnapshotOrgCity)).FontSize(10);
                if (!string.IsNullOrWhiteSpace(doc.SnapshotOrgRna))
                    left.Item().Text($"RNA : {doc.SnapshotOrgRna}").FontSize(9).FontColor("#374151");
                if (!string.IsNullOrWhiteSpace(doc.SnapshotOrgSiret))
                    left.Item().Text($"SIRET : {doc.SnapshotOrgSiret}").FontSize(9).FontColor("#374151");
            });
            row.ConstantItem(12);
            row.RelativeItem().Border(1).BorderColor("#E5E7EB").Padding(10).Column(right =>
            {
                right.Item().Text("Entreprise donatrice").SemiBold().FontSize(10).FontColor("#111827");
                right.Item().PaddingTop(4).Text(doc.SnapshotContactDisplayName).FontSize(10);
                if (!string.IsNullOrWhiteSpace(doc.SnapshotContactSiret))
                    right.Item().Text($"SIRET : {doc.SnapshotContactSiret}").FontSize(9).FontColor("#374151");
                if (!string.IsNullOrWhiteSpace(doc.SnapshotContactAddress))
                    right.Item().Text(doc.SnapshotContactAddress).FontSize(10);
            });
        });

        col.Item().PaddingTop(10).Border(1).BorderColor("#E5E7EB").Padding(10).Column(gift =>
        {
            gift.Item().Text("Détail du versement").SemiBold().FontSize(10).FontColor("#111827");
            gift.Item().PaddingTop(4).Text($"Montant : {amount}").FontSize(10);
            gift.Item().Text($"Date : {doc.SnapshotDonationDate:dd/MM/yyyy}").FontSize(10);
            gift.Item().Text($"Nature : {TranslateDonationType(doc.SnapshotDonationType)}").FontSize(10);
        });

        if (!string.IsNullOrWhiteSpace(giftText))
        {
            col.Item().PaddingTop(10).Border(1).BorderColor("#BFDBFE").Background("#EFF6FF").Padding(10).Column(msg =>
            {
                msg.Item().Text("Message personnalisé").SemiBold().FontSize(10).FontColor("#1E40AF");
                msg.Item().PaddingTop(4).Text(giftText).FontSize(10).FontColor("#374151");
            });
        }

        col.Item().PaddingTop(10).Background(Colors.Yellow.Lighten4).Padding(8).Text(
            "Ce reçu ouvre droit à une réduction d'impôt sur les sociétés de 60% du montant du versement, dans les limites prévues par l'article 238 bis du CGI.")
            .FontSize(9).Italic();

        col.Item().PaddingTop(12).Text("Date et signature de l'organisme").FontSize(9).FontColor("#6B7280");
        col.Item().PaddingTop(4);
        BuildSignature(col, data);
    }

    private static void BuildSignature(ColumnDescriptor col, PrintPageData data)
    {
        col.Item().AlignRight().Column(sig =>
        {
            if (data.SignatureBlock?.StoredPath != null
                && File.Exists(data.SignatureBlock.StoredPath))
            {
                sig.Item().AlignRight()
                    .Image(data.SignatureBlock.StoredPath)
                    .FitWidth()
                    .WithCompressionQuality(ImageCompressionQuality.High);
                sig.Item().PaddingVertical(4);
            }

            sig.Item().Text(DateTime.Now.ToString("dd MMMM yyyy",
                new System.Globalization.CultureInfo("fr-FR")))
                .FontSize(9).FontColor(Colors.Grey.Darken1);
        });
    }

    private static string FormatAddress(string? street, string? postalCode, string? city)
    {
        var parts = new[] { street, $"{postalCode} {city}".Trim() }
            .Where(p => !string.IsNullOrEmpty(p));
        return string.Join(", ", parts);
    }

    private static string TranslateDonationType(string type) => type switch
    {
        "financial" => "Don financier",
        "in_kind" => "Don en nature",
        "sponsoring" => "Sponsoring",
        _ => type
    };

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
