using Kalon.Back.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kalon.Back.Services.Mail;

internal static class MembershipCertificateDocumentLayout
{
    public static void Render(ColumnDescriptor col, PrintPageData data)
    {
        var doc = data.GeneratedDocument!;

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
        BuildSignature(col, data, false);
    }

    private static void BuildSignature(ColumnDescriptor col, PrintPageData data, bool includeDate = true)
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

            if (includeDate)
            {
                sig.Item().Text(DateTime.Now.ToString("dd MMMM yyyy",
                    new System.Globalization.CultureInfo("fr-FR")))
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static string FormatAddress(string? street, string? postalCode, string? city)
    {
        var parts = new[] { street, $"{postalCode} {city}".Trim() }
            .Where(p => !string.IsNullOrEmpty(p));
        return string.Join(", ", parts);
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var decoded = System.Net.WebUtility.HtmlDecode(html)
            .Replace('\u00A0', ' ');

        var withLineBreaks = System.Text.RegularExpressions.Regex.Replace(decoded, @"<(br|BR)\s*/?>", "\n");
        withLineBreaks = System.Text.RegularExpressions.Regex.Replace(withLineBreaks, @"</(p|div|li|h[1-6])>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var withoutTags = System.Text.RegularExpressions.Regex.Replace(withLineBreaks, "<.*?>", " ");
        var normalizedLines = System.Text.RegularExpressions.Regex.Replace(withoutTags, @"[ \t]{2,}", " ");
        var normalizedBreaks = System.Text.RegularExpressions.Regex.Replace(normalizedLines, @"\n{3,}", "\n\n");
        return normalizedBreaks.Trim();
    }
}
