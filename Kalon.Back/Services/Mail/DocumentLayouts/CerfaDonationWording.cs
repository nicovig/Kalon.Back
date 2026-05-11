using Kalon.Back.Models;
using System.Globalization;

namespace Kalon.Back.Services.Mail;

internal static class CerfaDonationWording
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static string FormatMoney(decimal amount) => amount.ToString("C", Fr);

    public static bool MultipleDonations(GeneratedDocument d) => d.SnapshotDonationCount > 1;

    public static string FormatPeriodLabel(GeneratedDocument d)
    {
        if (d.SnapshotDonationCount <= 0)
            return string.Empty;
        if (d.SnapshotDonationCount == 1)
            return $"le {d.SnapshotDonationDate.ToString("dd/MM/yyyy", Fr)}";
        var to = d.SnapshotDonationToDate ?? d.SnapshotDonationDate;
        if (d.SnapshotDonationDate.Date == to.Date)
            return $"le {d.SnapshotDonationDate.ToString("dd/MM/yyyy", Fr)} ({d.SnapshotDonationCount} versements)";
        return $"du {d.SnapshotDonationDate.ToString("dd/MM/yyyy", Fr)} au {to.ToString("dd/MM/yyyy", Fr)}";
    }

    public static string FormatCivilYearNumber(GeneratedDocument d) =>
        d.SnapshotDonationDate.Year.ToString(CultureInfo.InvariantCulture);
}
