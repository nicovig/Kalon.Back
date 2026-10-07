using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Configuration;
using Kalon.Back.Services.OrganizationAccess;
using Kalon.Back.Models;
using Kalon.Back.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

public class ContactControllerTests
{
    private static ContactController CreateController(ApplicationDbContext dbContext) =>
        new(
            dbContext,
            new UserOrganizationAccessService(dbContext),
            new PlanService(
                new HttpContextAccessor(),
                Options.Create(new PlanOptions())),
            new TagService(dbContext));

    private static void SetAuthenticatedUser(ControllerBase controller, Guid userId)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("sub", userId.ToString())
                ], "TestAuth"))
            }
        };
    }

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

        var result = await controller.GetAll(CancellationToken.None);

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
        SetAuthenticatedUser(controller, userId);
        var request = new ContactCreateRequest
        {
            Firstname = "New",
            Lastname = "Contact",
            Email = "new.contact@example.com"
        };

        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<ContactResponse>(created.Value);
        Assert.Equal("New", payload.Firstname);
        Assert.False(payload.IsEnterprise);
        Assert.False(payload.IsFamilyMembership);
        Assert.Null(payload.LastMembershipDate);
        Assert.Null(payload.MembershipEndDate);
    }

    [Fact]
    public async Task Create_PersistsMembershipFields()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var lastMembership = DateTime.UtcNow.Date.AddMonths(-1);
        var membershipEnd = DateTime.UtcNow.Date.AddMonths(11);

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.Create(new ContactCreateRequest
        {
            Firstname = "Member",
            Lastname = "Family",
            Email = "member@example.com",
            LastMembershipDate = lastMembership,
            MembershipEndDate = membershipEnd,
            IsFamilyMembership = true
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<ContactResponse>(created.Value);
        Assert.Equal(lastMembership, payload.LastMembershipDate);
        Assert.Equal(membershipEnd, payload.MembershipEndDate);
        Assert.True(payload.IsFamilyMembership);
    }

    [Fact]
    public async Task Update_PersistsMembershipFields()
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

        var lastMembership = DateTime.UtcNow.Date;
        var membershipEnd = DateTime.UtcNow.Date.AddYears(1);

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.Update(contact.Id, new ContactCreateRequest
        {
            Firstname = "After",
            Lastname = "Updated",
            Email = "after@example.com",
            LastMembershipDate = lastMembership,
            MembershipEndDate = membershipEnd,
            IsFamilyMembership = true
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ContactResponse>(ok.Value);
        Assert.Equal(lastMembership, payload.LastMembershipDate);
        Assert.Equal(membershipEnd, payload.MembershipEndDate);
        Assert.True(payload.IsFamilyMembership);
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
        SetAuthenticatedUser(controller, userId);
        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

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
        SetAuthenticatedUser(controller, userId);
        var result = await controller.GetById(contact.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ContactResponse>(ok.Value);
        Assert.Equal("John", payload.Firstname);
        Assert.Equal(contact.Id, payload.Id);
        Assert.Equal(0m, payload.TotalDonation);
        Assert.Null(payload.FirstDonationAt);
        Assert.Null(payload.FirstDonationAmount);
        Assert.Null(payload.LastDonation);
        Assert.Null(payload.LastDonationAmount);
        Assert.Equal(0m, payload.AverageDonationAmount);
        Assert.Equal(0, payload.DonationCount);
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
        SetAuthenticatedUser(controller, userId);
        var request = new ContactCreateRequest
        {
            Firstname = "After",
            Lastname = "Updated",
            Email = "after@example.com"
        };

        var result = await controller.Update(contact.Id, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ContactResponse>(ok.Value);
        Assert.Equal("After", payload.Firstname);
        Assert.False(payload.IsEnterprise);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenEmailAndPhoneMissing()
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
        SetAuthenticatedUser(controller, userId);
        var request = new ContactCreateRequest
        {
            Firstname = "After",
            Lastname = "Updated"
        };

        var result = await controller.Update(contact.Id, request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsNotFound_WhenOrganizationDoesNotExistForUser()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.GetAll(CancellationToken.None);

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
        SetAuthenticatedUser(controller, userId);

        var result = await controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IEnumerable<ContactResponse>>(ok.Value);
        var list = payload.ToList();

        Assert.Equal(2, list.Count);
        Assert.Equal("Newest", list[0].Firstname);
        Assert.Equal("Older", list[1].Firstname);
        Assert.DoesNotContain(list, c => c.Firstname == "OtherOrg");
    }

    [Fact]
    public async Task GetById_ReturnsComputedDonationAggregates()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var contact = CreateContact(Guid.NewGuid(), organizationId, "John", DateTime.UtcNow);
        var olderDonationDate = DateTime.UtcNow.Date.AddDays(-10);
        var latestDonationDate = DateTime.UtcNow.Date.AddDays(-2);

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        dbContext.Donations.AddRange(
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contact.Id,
                Amount = 10m,
                Date = olderDonationDate,
                DonationType = "financial",
                CreatedAt = DateTime.UtcNow
            },
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contact.Id,
                Amount = 25m,
                Date = latestDonationDate,
                DonationType = "financial",
                CreatedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.GetById(contact.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ContactResponse>(ok.Value);
        Assert.Equal(35m, payload.TotalDonation);
        Assert.Equal(olderDonationDate, payload.FirstDonationAt);
        Assert.Equal(10m, payload.FirstDonationAmount);
        Assert.Equal(latestDonationDate, payload.LastDonation);
        Assert.Equal(25m, payload.LastDonationAmount);
        Assert.Equal(17.5m, payload.AverageDonationAmount);
        Assert.Equal(2, payload.DonationCount);
    }

    [Fact]
    public async Task GetAll_ReturnsComputedDonationAggregates()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var contact = CreateContact(Guid.NewGuid(), organizationId, "Aggregated", DateTime.UtcNow);

        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Contacts.Add(contact);
        dbContext.Donations.AddRange(
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contact.Id,
                Amount = 5m,
                Date = DateTime.UtcNow.Date.AddDays(-5),
                DonationType = "financial",
                CreatedAt = DateTime.UtcNow
            },
            new Donation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                ContactId = contact.Id,
                Amount = 15m,
                Date = DateTime.UtcNow.Date.AddDays(-1),
                DonationType = "financial",
                CreatedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IEnumerable<ContactResponse>>(ok.Value);
        var aggregated = Assert.Single(payload);
        Assert.Equal(20m, aggregated.TotalDonation);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(-5), aggregated.FirstDonationAt);
        Assert.Equal(5m, aggregated.FirstDonationAmount);
        Assert.Equal(2, aggregated.DonationCount);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(-1), aggregated.LastDonation);
        Assert.Equal(15m, aggregated.LastDonationAmount);
        Assert.Equal(10m, aggregated.AverageDonationAmount);
    }

    [Fact]
    public async Task CreateBulk_ReturnsCreatedIds_WhenRequestIsValid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.CreateBulk(new ContactBulkCreateRequest
        {
            Items =
            [
                new ContactCreateRequest
                {
                    Firstname = "A",
                    Lastname = "One",
                    Email = "a@example.com"
                },
                new ContactCreateRequest
                {
                    Firstname = "B",
                    Lastname = "Two",
                    Email = "b@example.com"
                }
            ]
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ContactBulkCreateResponse>(ok.Value);
        Assert.Equal(2, payload.CreatedCount);
        Assert.Equal(2, payload.CreatedIds.Count);
    }

    [Fact]
    public async Task CreateBulk_ReturnsBadRequest_WhenAnyItemInvalid()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.CreateBulk(new ContactBulkCreateRequest
        {
            Items =
            [
                new ContactCreateRequest
                {
                    Firstname = "A",
                    Lastname = "One"
                }
            ]
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_AssignsTags_WhenTagIdsProvided()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "VIP",
            Color = "#FF0000",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.Create(new ContactCreateRequest
        {
            Firstname = "Tagged",
            Lastname = "Contact",
            Email = "tagged@example.com",
            TagIds = [tag.Id]
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<ContactResponse>(created.Value);
        var assigned = Assert.Single(payload.Tags);
        Assert.Equal(tag.Id, assigned.Id);
        Assert.Equal("VIP", assigned.Name);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTagIdsUnknown()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organization = CreateOrganization(Guid.NewGuid(), userId, user);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.Create(new ContactCreateRequest
        {
            Firstname = "Tagged",
            Lastname = "Contact",
            Email = "tagged@example.com",
            TagIds = [Guid.NewGuid()]
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Update_ReplacesTags_WhenTagIdsProvided()
    {
        using var dbContext = CreateDbContext(Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "owner@example.com");
        var organizationId = Guid.NewGuid();
        var organization = CreateOrganization(organizationId, userId, user);
        var tagOld = new Tag
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Old",
            CreatedAt = DateTime.UtcNow
        };
        var tagNew = new Tag
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "New",
            CreatedAt = DateTime.UtcNow
        };
        var contact = CreateContact(Guid.NewGuid(), organizationId, "Before", DateTime.UtcNow);
        contact.Tags.Add(tagOld);
        dbContext.Users.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.Tags.AddRange(tagOld, tagNew);
        dbContext.Contacts.Add(contact);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);
        SetAuthenticatedUser(controller, userId);
        var result = await controller.Update(contact.Id, new ContactCreateRequest
        {
            Firstname = "After",
            Lastname = "Updated",
            Email = "after@example.com",
            TagIds = [tagNew.Id]
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ContactResponse>(ok.Value);
        var assigned = Assert.Single(payload.Tags);
        Assert.Equal(tagNew.Id, assigned.Id);
        Assert.Equal("New", assigned.Name);
    }
}
