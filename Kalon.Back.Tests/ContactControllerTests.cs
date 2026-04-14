using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.Services.OrganizationAccess;
using Kalon.Back.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class ContactControllerTests
{
    private static ContactController CreateController(ApplicationDbContext dbContext) =>
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

    private static Contact CreateContact(Guid id, Guid organizationId, string firstname, DateTime createdAt)
    {
        return new Contact
        {
            Id = id,
            OrganizationId = organizationId,
            Kind = ContactKinds.Donor,
            Firstname = firstname,
            Lastname = "Lastname",
            Email = $"{firstname.ToLowerInvariant()}@example.com",
            CreatedAt = createdAt
        };
    }

    [Fact]
    public async Task GetAll_ReturnsBadRequest_WhenUserIdIsEmpty()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var controller = CreateController(dbContext);

        var result = await controller.GetAll(Guid.Empty, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenRequestIsValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new Contact
        {
            Kind = ContactKinds.Donor,
            Firstname = "New",
            Lastname = "Contact",
            Email = "new.contact@example.com"
        };

        var result = await controller.Create(userId, request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<Contact>(created.Value);
        Assert.Equal("New", payload.Firstname);
        Assert.Equal(ContactKinds.Donor, payload.Kind);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenContactDoesNotExist()
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
    public async Task GetById_ReturnsContact_WhenExistsInUserOrganization()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var contact = CreateContact(Guid.NewGuid(), organizationId, "John", DateTime.UtcNow);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var result = await controller.GetById(userId, contact.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<Contact>(ok.Value);
        Assert.Equal("John", payload.Firstname);
        Assert.Equal(contact.Id, payload.Id);
    }

    [Fact]
    public async Task Update_ReturnsOk_AndUpdatesContact_WhenValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var contact = CreateContact(Guid.NewGuid(), organizationId, "Before", DateTime.UtcNow);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new Contact
        {
            Kind = ContactKinds.Member,
            Firstname = "After",
            Lastname = "Updated",
            Email = "after@example.com"
        };

        var result = await controller.Update(userId, contact.Id, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<Contact>(ok.Value);
        Assert.Equal("After", payload.Firstname);
        Assert.Equal(ContactKinds.Member, payload.Kind);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenKindInvalid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var contact = CreateContact(Guid.NewGuid(), organizationId, "Before", DateTime.UtcNow);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        var request = new Contact
        {
            Kind = "invalid_kind",
            Firstname = "After",
            Lastname = "Updated"
        };

        var result = await controller.Update(userId, contact.Id, request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsNotFound_WhenOrganizationDoesNotExistForUser()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var controller = CreateController(dbContext);

        var result = await controller.GetAll(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyContactsForUserOrganization_OrderedByCreatedAtDesc()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);

        var otherUser = CreateUser(Guid.NewGuid(), "other@example.com");
        var otherOrganizationId = Guid.NewGuid();
        var otherOrganization = CreateOrganization(otherOrganizationId, otherUser.Id, otherUser);

        dbContext.Users.AddRange(user, otherUser);
        dbContext.Organizations.AddRange(organization, otherOrganization);
        dbContext.Contacts.AddRange(
            CreateContact(Guid.NewGuid(), organizationId, "Older", DateTime.UtcNow.AddDays(-2)),
            CreateContact(Guid.NewGuid(), organizationId, "Newest", DateTime.UtcNow.AddDays(-1)),
            CreateContact(Guid.NewGuid(), otherOrganizationId, "OtherOrg", DateTime.UtcNow)
        );
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.GetAll(userId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IEnumerable<Contact>>(ok.Value);
        var list = payload.ToList();

        Assert.Equal(2, list.Count);
        Assert.Equal("Newest", list[0].Firstname);
        Assert.Equal("Older", list[1].Firstname);
        Assert.DoesNotContain(list, c => c.Firstname == "OtherOrg");
    }
}
