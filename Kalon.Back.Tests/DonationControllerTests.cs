using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.Dtos.Donation;
using Kalon.Back.Services.OrganizationAccess;
using Kalon.Back.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class DonationControllerTests
{
    private const int DefaultPageSize = 50;

    private static DonationController CreateController(ApplicationDbContext dbContext) =>
        new(dbContext, new UserOrganizationAccessService(dbContext));

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static User CreateUser(Guid id, string email)
    {
        return new User
        {
            Id = id,
            MeranId = Guid.NewGuid(),
            Firstname = "Test",
            Lastname = "User",
            Email = email,
            AssociationName = "Asso",
            PasswordHash = "hash",
            Salt = "salt"
        };
    }

    private static Organization CreateOrganization(Guid id, Guid userId, User user)
    {
        return new Organization
        {
            Id = id,
            Name = "Test Organization",
            Email = "org@test.local",
            UserId = userId,
            User = user,
            RNA = "W442009999",
            SIRET = "12345678901234",
            FiscalStatus = FiscalStatus.GeneralInterest,
            DefaultReceiptFrequency = ReceiptFrequency.Annually,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Contact CreateContact(Guid id, Guid organizationId, string firstname, string lastname)
    {
        return new Contact
        {
            Id = id,
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Status = ContactStatuses.Active,
            Firstname = firstname,
            Lastname = lastname,
            Email = $"{firstname.ToLowerInvariant()}@example.com",
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Donation CreateDonation(Guid id, Guid organizationId, Guid contactId, decimal amount, DateTime date, string type)
    {
        return new Donation
        {
            Id = id,
            OrganizationId = organizationId,
            ContactId = contactId,
            Amount = amount,
            Date = date,
            DonationType = type,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenRequestIsValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var contact = CreateContact(Guid.NewGuid(), organizationId, "Donor", "One");
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new DonationUpsertRequest
        {
            ContactId = contact.Id,
            Amount = 120.50m,
            Date = DateTime.UtcNow.Date,
            DonationType = "financial",
            PaymentMethod = "bank_transfer",
            Notes = "First donation",
            IsAnonymous = false
        };

        var result = await controller.Create(userId, request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<DonationDetailsResponse>(created.Value);
        Assert.Equal(120.50m, payload.Amount);
        Assert.Equal(contact.Id, payload.ContactId);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenContactNotInOrganization()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);

        var otherUser = CreateUser(Guid.NewGuid(), "other@example.com");
        var otherOrganizationId = Guid.NewGuid();
        var otherOrganization = CreateOrganization(otherOrganizationId, otherUser.Id, otherUser);
        var otherContact = CreateContact(Guid.NewGuid(), otherOrganizationId, "Other", "Contact");

        dbContext.Users.AddRange(user, otherUser);
        dbContext.Organizations.AddRange(organization, otherOrganization);
        dbContext.Contacts.Add(otherContact);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new DonationUpsertRequest
        {
            ContactId = otherContact.Id,
            Amount = 100m,
            Date = DateTime.UtcNow.Date,
            DonationType = "financial"
        };

        var result = await controller.Create(userId, request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyDonationsForUserOrganization()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var contact = CreateContact(Guid.NewGuid(), organizationId, "John", "Doe");

        var otherUser = CreateUser(Guid.NewGuid(), "other@example.com");
        var otherOrganizationId = Guid.NewGuid();
        var otherOrganization = CreateOrganization(otherOrganizationId, otherUser.Id, otherUser);
        var otherContact = CreateContact(Guid.NewGuid(), otherOrganizationId, "Jane", "Other");

        dbContext.Users.AddRange(user, otherUser);
        dbContext.Organizations.AddRange(organization, otherOrganization);
        dbContext.Contacts.AddRange(contact, otherContact);
        dbContext.Donations.AddRange(
            CreateDonation(Guid.NewGuid(), organizationId, contact.Id, 50m, DateTime.UtcNow.AddDays(-2), "financial"),
            CreateDonation(Guid.NewGuid(), organizationId, contact.Id, 80m, DateTime.UtcNow.AddDays(-1), "financial"),
            CreateDonation(Guid.NewGuid(), otherOrganizationId, otherContact.Id, 100m, DateTime.UtcNow, "financial")
        );
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetAll(
            userId,
            null,
            null,
            null,
            null,
            null,
            null,
            1,
            DefaultPageSize,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<DonationPagedResponse>(ok.Value);
        var list = payload.Items.ToList();

        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(1, payload.TotalPages);
        Assert.Equal(2, list.Count);
        Assert.Equal(80m, list[0].Amount);
        Assert.Equal(50m, list[1].Amount);
    }

    [Fact]
    public async Task GetAll_ReturnsPagedSlice_WhenPageSizeIsOne()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var contact = CreateContact(Guid.NewGuid(), organizationId, "John", "Doe");
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        dbContext.Donations.AddRange(
            CreateDonation(Guid.NewGuid(), organizationId, contact.Id, 50m, DateTime.UtcNow.AddDays(-2), "financial"),
            CreateDonation(Guid.NewGuid(), organizationId, contact.Id, 80m, DateTime.UtcNow.AddDays(-1), "financial"));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetAll(
            userId,
            null,
            null,
            null,
            null,
            null,
            null,
            2,
            1,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<DonationPagedResponse>(ok.Value);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(2, payload.TotalPages);
        Assert.Single(payload.Items);
        Assert.Equal(50m, payload.Items[0].Amount);
    }

    [Fact]
    public async Task GetAll_FiltersByDonationType_CaseInsensitive()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var contact = CreateContact(Guid.NewGuid(), organizationId, "John", "Doe");
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        dbContext.Donations.AddRange(
            CreateDonation(Guid.NewGuid(), organizationId, contact.Id, 10m, DateTime.UtcNow, "financial"),
            CreateDonation(Guid.NewGuid(), organizationId, contact.Id, 20m, DateTime.UtcNow, "in_kind"));
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetAll(
            userId,
            null,
            null,
            "In_Kind",
            null,
            null,
            null,
            1,
            DefaultPageSize,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<DonationPagedResponse>(ok.Value);
        Assert.Equal(1, payload.TotalCount);
        Assert.Equal("in_kind", payload.Items[0].DonationType);
    }

    [Fact]
    public async Task GetAll_ReturnsBadRequest_WhenPageInvalid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetAll(
            userId,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            DefaultPageSize,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenDonationMissing()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetById(userId, Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenRequestIsValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var contact = CreateContact(Guid.NewGuid(), organizationId, "Donor", "One");
        var donation = CreateDonation(Guid.NewGuid(), organizationId, contact.Id, 30m, DateTime.UtcNow.AddDays(-10), "financial");

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        dbContext.Donations.Add(donation);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new DonationUpsertRequest
        {
            ContactId = contact.Id,
            Amount = 75m,
            Date = DateTime.UtcNow.Date,
            DonationType = "sponsoring",
            PaymentMethod = "check",
            Notes = "Updated donation",
            IsAnonymous = true
        };

        var result = await controller.Update(userId, donation.Id, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<DonationDetailsResponse>(ok.Value);
        Assert.Equal(75m, payload.Amount);
        Assert.Equal("sponsoring", payload.DonationType);
        Assert.True(payload.IsAnonymous);
    }
}
