using Kalon.Back.Services;

namespace Kalon.Back.Tests;

public class TaxReceiptPeriodHelperTests
{
    [Fact]
    public void ContactNeedsCerfaForPeriod_ReturnsFalseWhenAllFinancialDonationsCoveredByCerfaSnapshot()
    {
        var period = TaxReceiptPeriodHelper.Create(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        var donations = new[]
        {
            new DonationReceiptRow(
                Guid.NewGuid(),
                new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                TaxReceiptPeriodHelper.FinancialDonationType,
                null,
                null)
        };

        var cerfaCoverage = new List<TaxReceiptPeriod>
        {
            TaxReceiptPeriodHelper.Create(
                new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc))
        };

        Assert.False(TaxReceiptPeriodHelper.ContactNeedsCerfaForPeriod(period, donations, cerfaCoverage));
    }

    [Fact]
    public void ContactNeedsCerfaForPeriod_ReturnsTrueWhenUncoveredFinancialDonationInPeriod()
    {
        var period = TaxReceiptPeriodHelper.Create(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        var donations = new[]
        {
            new DonationReceiptRow(
                Guid.NewGuid(),
                new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                TaxReceiptPeriodHelper.FinancialDonationType,
                null,
                null)
        };

        Assert.True(TaxReceiptPeriodHelper.ContactNeedsCerfaForPeriod(period, donations, []));
    }
}
