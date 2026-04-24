using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Services.Notification;

public class NotificationDashboardService(ApplicationDbContext dbContext) : INotificationDashboardService
{
    private const int DefaultNewDurationDays = 30;
    private const int DefaultToRemindAfterMonths = 12;
    private const int DefaultInactiveAfterMonths = 24;

    public async Task<NotificationDashboardResponse> GetDashboardAsync(Guid organizationId, CancellationToken cancellationToken)
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

        var donations = await dbContext.Donations
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => new
            {
                x.ContactId,
                x.Date,
                x.GeneratedDocumentId
            })
            .ToListAsync(cancellationToken);

        var physicalLettersToSendCount = await dbContext.MailLogs
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && !x.IsEmail && x.Status == MailLogStatuses.Printed)
            .CountAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var newDurationDays = settings?.NewDurationDays ?? DefaultNewDurationDays;
        var toRemindAfterMonths = settings?.ToRemindAfterMonths ?? DefaultToRemindAfterMonths;
        var inactiveAfterMonths = settings?.InactiveAfterMonths ?? DefaultInactiveAfterMonths;

        var donationByContact = donations
            .GroupBy(x => x.ContactId)
            .ToDictionary(g => g.Key, g => g.ToList());

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

            var effectiveFrequency = ResolveReceiptFrequency(contact.PreferredFrequencySendingReceipt,
                organization.DefaultReceiptFrequency);
            var uneditedDonations = contactDonations
                .Where(x => x.GeneratedDocumentId is null)
                .Select(x => x.Date)
                .ToList();
            if (IsReceiptDue(effectiveFrequency, uneditedDonations, now))
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
            PhysicalLettersToSendCount = physicalLettersToSendCount
        };
    }

    private static bool IsReceiptDue(string frequency, List<DateTime> uneditedDonationDates, DateTime now)
    {
        if (uneditedDonationDates.Count == 0)
            return false;

        if (frequency is "instantly" or "onetime")
            return true;

        var boundary = frequency switch
        {
            "monthly" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            "quarterly" => QuarterStartUtc(now),
            "semesterly" => SemesterStartUtc(now),
            "yearly" => new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        return uneditedDonationDates.Any(date => date < boundary);
    }

    private static DateTime QuarterStartUtc(DateTime now)
    {
        var month = ((now.Month - 1) / 3) * 3 + 1;
        return new DateTime(now.Year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime SemesterStartUtc(DateTime now)
    {
        var month = now.Month <= 6 ? 1 : 7;
        return new DateTime(now.Year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

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
