using Kalon.Back.Configuration;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Services;

public interface IAdminOnboardingService
{
    Task<AdminOnboardingResponse> OnboardAsync(AdminOnboardingRequest request, CancellationToken ct);
}

public class AdminOnboardingService(
    ApplicationDbContext db,
    IPasswordService passwordService,
    MeranClient meranClient,
    IOnboardingMailService onboardingMailService,
    IOptions<MeranOptions> meranOptions) : IAdminOnboardingService
{
    private readonly MeranOptions _meranOptions = meranOptions.Value;

    public async Task<AdminOnboardingResponse> OnboardAsync(AdminOnboardingRequest request, CancellationToken ct)
    {
        if (_meranOptions.ApplicationId == Guid.Empty)
            throw new InvalidOperationException("Meran:ApplicationId is not configured.");

        if (await db.Users.AnyAsync(u => u.Email == request.Email.Trim().ToLower(), ct))
            throw new InvalidOperationException($"Un compte existe déjà pour {request.Email}.");

        var plainPassword = passwordService.GeneratePassword();
        var salt = passwordService.GenerateSalt();
        var hash = passwordService.HashPassword(plainPassword, salt);

        var meranId = await meranClient.CreateUserAsync(
            _meranOptions.ApplicationId,
            new MeranCreateUserRequest
            {
                Name = $"{request.Lastname.Trim()} {request.Firstname.Trim()}",
                Email = request.Email.Trim().ToLower(),
                Plan = PlanTypes.Free
            },
            ct);

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            MeranId = meranId,
            Firstname = request.Firstname.Trim(),
            Lastname = request.Lastname.Trim(),
            Email = request.Email.Trim().ToLower(),
            AssociationName = request.AssociationName.Trim(),
            PasswordHash = hash,
            Salt = salt,
            Role = "organization_master"
        };
        db.Users.Add(user);

        var orgId = Guid.NewGuid();
        var org = new Organization
        {
            Id = orgId,
            UserId = userId,
            Name = request.AssociationName.Trim(),
            Email = request.Email.Trim().ToLower(),
            Phone = request.Phone?.Trim(),
            Website = request.Website?.Trim(),
            RNA = request.RNA?.Trim() ?? string.Empty,
            SIRET = string.Empty,
            FiscalStatus = Models.FiscalStatus.GeneralInterest,
            DefaultReceiptFrequency = ReceiptFrequency.Annually,
            CachedPlan = PlanTypes.Free,
            CachedPlanUpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            SendingPreferences = ["message", "tax_receipt", "payment_attestation", "membership_certificate"],
            ContactLinkingMode = ContactLinkingModes.None,
            ContactStatusSettings = new ContactStatusSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                NewDurationDays = DefaultTagValue.DefaultNewDurationDays,
                ToRemindAfterMonths = DefaultTagValue.DefaultToRemindAfterMonths,
                InactiveAfterMonths = DefaultTagValue.DefaultInactiveAfterMonths,
                CreatedAt = DateTime.UtcNow
            }
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync(ct);

        await onboardingMailService.SendWelcomeAsync(new WelcomeMailData
        {
            Firstname = request.Firstname.Trim(),
            Email = request.Email.Trim().ToLower(),
            AssociationName = request.AssociationName.Trim(),
            TemporaryPassword = plainPassword
        }, ct);

        return new AdminOnboardingResponse
        {
            UserId = userId,
            OrganizationId = orgId,
            Email = request.Email.Trim().ToLower(),
            TemporaryPassword = plainPassword
        };
    }
}
