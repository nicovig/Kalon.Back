using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Services;

public interface INotificationDashboardService
{
    Task<NotificationDashboardResponse> GetDashboardAsync(
        Guid organizationId,
        CancellationToken cancellationToken,
        DateTime? taxReceiptPeriodFrom = null,
        DateTime? taxReceiptPeriodTo = null);
}

public class NotificationDashboardService(ApplicationDbContext dbContext) : INotificationDashboardService
{
    public async Task<NotificationDashboardResponse> GetDashboardAsync(
        Guid organizationId,
        CancellationToken cancellationToken,
        DateTime? taxReceiptPeriodFrom = null,
        DateTime? taxReceiptPeriodTo = null)
    {
        var settings = await dbContext.ContactStatusSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .Where(x => x.Id == organizationId)
            .Select(x => new { x.DefaultReceiptFrequency })
            .FirstAsync(cancellationToken);

        var contacts = await dbContext.Contacts
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && !x.IsOut)
            .Select(x => new
            {
                x.Id,
                x.Firstname,
                x.Lastname,
                x.CreatedAt,
                x.PreferredFrequencySendingReceipt
            })
            .ToListAsync(cancellationToken);

        var donationRows = await dbContext.Donations
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => new DonationReceiptRow(
                x.ContactId,
                x.Date,
                x.DonationType,
                x.GeneratedDocumentId,
                x.GeneratedDocument != null ? x.GeneratedDocument.DocumentType : null))
            .ToListAsync(cancellationToken);

        var cerfaDocuments = await dbContext.GeneratedDocuments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId
                        && (x.DocumentType == DocumentType.Cerfa11580
                            || x.DocumentType == DocumentType.Cerfa16216))
            .Select(x => new
            {
                x.Id,
                x.SnapshotContactDisplayName,
                x.SnapshotDonationDate,
                x.SnapshotDonationToDate,
                DonationContactIds = x.Donations.Select(d => d.ContactId).ToList()
            })
            .ToListAsync(cancellationToken);

        var cerfaIds = cerfaDocuments.Select(x => x.Id).ToList();
        var mailLogRows = cerfaIds.Count == 0
            ? []
            : await dbContext.MailLogs
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId
                            && x.GeneratedDocumentId != null
                            && cerfaIds.Contains(x.GeneratedDocumentId.Value))
                .Select(x => new { GeneratedDocumentId = x.GeneratedDocumentId!.Value, x.ContactId })
                .ToListAsync(cancellationToken);
        var mailLogContactsByCerfa = mailLogRows
            .GroupBy(x => x.GeneratedDocumentId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ContactId).Distinct().ToList());

        var physicalLettersToSendCount = await dbContext.MailLogs
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && !x.IsEmail && x.Status == MailLogStatuses.Printed)
            .CountAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var newDurationDays = settings?.NewDurationDays ?? DefaultTagValue.DefaultNewDurationDays;
        var toRemindAfterMonths = settings?.ToRemindAfterMonths ?? DefaultTagValue.DefaultToRemindAfterMonths;
        var inactiveAfterMonths = settings?.InactiveAfterMonths ?? DefaultTagValue.DefaultInactiveAfterMonths;

        var defaultFrequency = ResolveReceiptFrequency(null, organization.DefaultReceiptFrequency);
        var taxReceiptPeriod = TaxReceiptPeriodHelper.ResolveOrDefault(
            taxReceiptPeriodFrom, taxReceiptPeriodTo, defaultFrequency, now);

        var donationByContact = donationRows
            .GroupBy(x => x.ContactId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var contactDisplayNames = contacts.ToDictionary(
            c => c.Id,
            c => $"{c.Firstname} {c.Lastname}".Trim());

        var cerfaCoverageRows = cerfaDocuments
            .Select(x => new CerfaDocumentCoverageRow(
                x.Id,
                x.SnapshotContactDisplayName,
                x.SnapshotDonationDate,
                x.SnapshotDonationToDate,
                x.DonationContactIds))
            .ToList();
        var cerfaPeriodsByContact = BuildCerfaCoveragePeriodsByContact(
            cerfaCoverageRows, mailLogContactsByCerfa, contactDisplayNames);

        var contactsToRemind = new List<NotificationContactItem>();
        var contactsToSendTaxReceipts = new List<NotificationContactItem>();

        foreach (var contact in contacts)
        {
            var contactDonations = donationByContact.GetValueOrDefault(contact.Id, []);
            var lastDonationDate = contactDonations
                .OrderByDescending(x => x.Date)
                .Select(x => (DateTime?)x.Date)
                .FirstOrDefault();

            var isNew = contact.CreatedAt >= now.AddDays(-newDurationDays);
            var referenceDate = lastDonationDate ?? contact.CreatedAt;
            var isInactive = referenceDate < now.AddMonths(-inactiveAfterMonths);
            var isToRemind = !isNew && !isInactive && referenceDate < now.AddMonths(-toRemindAfterMonths);
            if (isToRemind)
                contactsToRemind.Add(new NotificationContactItem
                {
                    ContactId = contact.Id,
                    DisplayName = $"{contact.Firstname} {contact.Lastname}".Trim()
                });

            var contactCerfaPeriods = cerfaPeriodsByContact.GetValueOrDefault(contact.Id, []);

            if (TaxReceiptPeriodHelper.ContactNeedsCerfaForPeriod(
                    taxReceiptPeriod, contactDonations, contactCerfaPeriods))
            {
                contactsToSendTaxReceipts.Add(new NotificationContactItem
                {
                    ContactId = contact.Id,
                    DisplayName = $"{contact.Firstname} {contact.Lastname}".Trim()
                });
            }
        }

        return new NotificationDashboardResponse
        {
            ContactsToRemind = contactsToRemind,
            ContactsToSendTaxReceipts = contactsToSendTaxReceipts,
            PhysicalLettersToSendCount = physicalLettersToSendCount,
            TaxReceiptPeriodFrom = taxReceiptPeriod.From,
            TaxReceiptPeriodTo = taxReceiptPeriod.To
        };
    }

    private static Dictionary<Guid, List<TaxReceiptPeriod>> BuildCerfaCoveragePeriodsByContact(
        IEnumerable<CerfaDocumentCoverageRow> cerfaDocuments,
        IReadOnlyDictionary<Guid, List<Guid>> mailLogContactsByCerfa,
        IReadOnlyDictionary<Guid, string> contactDisplayNames)
    {
        var result = new Dictionary<Guid, List<TaxReceiptPeriod>>();

        foreach (var cerfa in cerfaDocuments)
        {
            var period = TaxReceiptPeriodHelper.Create(
                cerfa.SnapshotDonationDate,
                cerfa.SnapshotDonationToDate ?? cerfa.SnapshotDonationDate);

            var contactIds = new List<Guid>(cerfa.DonationContactIds);
            if (contactIds.Count == 0)
                contactIds.AddRange(mailLogContactsByCerfa.GetValueOrDefault(cerfa.Id, []));
            if (contactIds.Count == 0)
            {
                var matched = contactDisplayNames
                    .Where(c => string.Equals(c.Value, cerfa.SnapshotContactDisplayName, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Key)
                    .ToList();
                contactIds.AddRange(matched);
            }

            foreach (var contactId in contactIds.Distinct())
            {
                if (!result.TryGetValue(contactId, out var periods))
                {
                    periods = [];
                    result[contactId] = periods;
                }

                periods.Add(period);
            }
        }

        return result;
    }

    private sealed record CerfaDocumentCoverageRow(
        Guid Id,
        string SnapshotContactDisplayName,
        DateTime SnapshotDonationDate,
        DateTime? SnapshotDonationToDate,
        List<Guid> DonationContactIds);

    private static string ResolveReceiptFrequency(string? preferredFrequency, ReceiptFrequency defaultFrequency)
    {
        if (!string.IsNullOrWhiteSpace(preferredFrequency))
            return preferredFrequency.Trim().ToLowerInvariant();

        return defaultFrequency switch
        {
            ReceiptFrequency.Monthly => "monthly",
            ReceiptFrequency.Quarterly => "quarterly",
            ReceiptFrequency.HalfYearly => "semesterly",
            ReceiptFrequency.Annually => "yearly",
            ReceiptFrequency.OneTime => "instantly",
            _ => "yearly"
        };
    }

}
