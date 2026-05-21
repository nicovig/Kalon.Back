namespace Kalon.Back.Services;

public sealed record TaxReceiptPeriod(DateTime From, DateTime To)
{
    public bool Contains(DateTime donationDate) =>
        donationDate.Date >= From.Date && donationDate.Date <= To.Date;
}

public static class TaxReceiptPeriodHelper
{
    public const string FinancialDonationType = "financial";

    public static TaxReceiptPeriod Create(DateTime from, DateTime to)
    {
        var period = new TaxReceiptPeriod(
            DateTime.SpecifyKind(from.Date, DateTimeKind.Utc),
            DateTime.SpecifyKind(to.Date, DateTimeKind.Utc));
        if (period.From > period.To)
            throw new ArgumentException("La date de début de période doit être antérieure ou égale à la date de fin.");
        return period;
    }

    public static TaxReceiptPeriod ResolveOrDefault(
        DateTime? from,
        DateTime? to,
        string frequency,
        DateTime now)
    {
        if (from.HasValue && to.HasValue)
            return Create(from.Value, to.Value);
        if (from.HasValue || to.HasValue)
            throw new ArgumentException("Les deux bornes de période (from et to) sont requises.");
        return GetDefaultPeriod(frequency, now);
    }

    public static TaxReceiptPeriod GetDefaultPeriod(string frequency, DateTime now)
    {
        var utcNow = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        return frequency switch
        {
            "monthly" => CreatePreviousMonth(utcNow),
            "quarterly" => Create(
                QuarterStartUtc(utcNow.AddMonths(-3)),
                QuarterStartUtc(utcNow).AddDays(-1)),
            "semesterly" => Create(
                SemesterStartUtc(utcNow.AddMonths(-6)),
                SemesterStartUtc(utcNow).AddDays(-1)),
            "instantly" or "onetime" => Create(
                new DateTime(utcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                utcNow),
            _ => Create(
                new DateTime(utcNow.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(utcNow.Year - 1, 12, 31, 0, 0, 0, DateTimeKind.Utc))
        };
    }

    public static bool IsCerfaDocumentType(string? documentType) =>
        documentType is Models.DocumentType.Cerfa11580 or Models.DocumentType.Cerfa16216;

    public static bool IsDonationCoveredByCerfa(
        DateTime donationDate,
        Guid? generatedDocumentId,
        string? linkedDocumentType,
        IReadOnlyList<TaxReceiptPeriod> cerfaCoveragePeriods)
    {
        if (generatedDocumentId.HasValue && IsCerfaDocumentType(linkedDocumentType))
            return true;

        return cerfaCoveragePeriods.Any(p => p.Contains(donationDate));
    }

    public static bool ContactNeedsCerfaForPeriod(
        TaxReceiptPeriod period,
        IEnumerable<DonationReceiptRow> donations,
        IReadOnlyList<TaxReceiptPeriod> cerfaCoveragePeriods)
    {
        var financialInPeriod = donations
            .Where(d => d.DonationType == FinancialDonationType && period.Contains(d.Date))
            .ToList();

        if (financialInPeriod.Count == 0)
            return false;

        return financialInPeriod.Any(d =>
            !IsDonationCoveredByCerfa(d.Date, d.GeneratedDocumentId, d.LinkedDocumentType, cerfaCoveragePeriods));
    }

    private static DateTime QuarterStartUtc(DateTime date)
    {
        var month = ((date.Month - 1) / 3) * 3 + 1;
        return new DateTime(date.Year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime SemesterStartUtc(DateTime date)
    {
        var month = date.Month <= 6 ? 1 : 7;
        return new DateTime(date.Year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static TaxReceiptPeriod CreatePreviousMonth(DateTime utcNow)
    {
        var firstOfCurrentMonth = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastOfPreviousMonth = firstOfCurrentMonth.AddDays(-1);
        var firstOfPreviousMonth = new DateTime(
            lastOfPreviousMonth.Year, lastOfPreviousMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return Create(firstOfPreviousMonth, lastOfPreviousMonth);
    }
}

public sealed record DonationReceiptRow(
    Guid ContactId,
    DateTime Date,
    string DonationType,
    Guid? GeneratedDocumentId,
    string? LinkedDocumentType);
