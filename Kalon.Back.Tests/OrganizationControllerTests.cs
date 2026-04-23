using System.Security.Claims;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class OrganizationControllerTests
{
    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static OrganizationController CreateController(ApplicationDbContext db, Guid organizationId)
    {
        var controller = new OrganizationController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("organization_id", organizationId.ToString())
                ], "TestAuth"))
            }
        };
        return controller;
    }

    private static Organization CreateOrganization(Guid organizationId) => new()
    {
        Id = organizationId,
        Name = "Asso Demo",
        Email = "contact@asso.org",
        UserId = Guid.NewGuid(),
        User = new User
        {
            Id = Guid.NewGuid(),
            MeranId = Guid.NewGuid(),
            Firstname = "Owner",
            Lastname = "User",
            Email = "owner@asso.org",
            AssociationName = "Asso Demo",
            PasswordHash = "hash",
            Salt = "salt"
        },
        RNA = "W442009999",
        SIRET = "12345678901234",
        FiscalStatus = FiscalStatus.GeneralInterest,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Get_ReturnsTypedResponse_WhenOrganizationExists()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        await db.SaveChangesAsync();

        var controller = CreateController(db, organizationId);
        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<OrganizationResponseDto>(ok.Value);
        Assert.Equal("Asso Demo", payload.Name);
        Assert.Equal("contact@asso.org", payload.Email);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenFiscalStatusInvalid()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        await db.SaveChangesAsync();

        var controller = CreateController(db, organizationId);
        var result = await controller.Update(new OrganizationUpdateRequestDto
        {
            Name = "Asso Demo",
            Email = "contact@asso.org",
            RNA = "W442009999",
            SIRET = "12345678901234",
            FiscalStatus = "invalid"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<ApiMessageResponse>(badRequest.Value);
        Assert.Equal("Statut fiscal invalide.", payload.Message);
    }

    [Fact]
    public async Task Update_UsesRequestDtoWithoutRequiringModelNavigations()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        await db.SaveChangesAsync();

        var controller = CreateController(db, organizationId);
        var result = await controller.Update(new OrganizationUpdateRequestDto
        {
            Name = "Asso Updatee",
            Email = "nouveau@asso.org",
            RNA = "W000000001",
            SIRET = "11111111111111",
            FiscalStatus = FiscalStatus.PublicUtility,
            SenderName = "Equipe Asso"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<OrganizationResponseDto>(ok.Value);
        Assert.Equal("Asso Updatee", payload.Name);
        Assert.Equal("nouveau@asso.org", payload.Email);
        Assert.Equal("W000000001", payload.RNA);
        Assert.Equal("11111111111111", payload.SIRET);
        Assert.Equal(FiscalStatus.PublicUtility, payload.FiscalStatus);
        Assert.Equal("Equipe Asso", payload.SenderName);
    }

    [Fact]
    public async Task UpdateStatusSettings_CreatesSettingsAndReturnsTypedDto()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(CreateOrganization(organizationId));
        await db.SaveChangesAsync();

        var controller = CreateController(db, organizationId);
        var result = await controller.UpdateStatusSettings(new OrganizationStatusSettingsUpsertRequestDto
        {
            NewDurationDays = 45,
            ToRemindAfterMonths = 9,
            InactiveAfterMonths = 18
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ContactStatusSettingsResponseDto>(ok.Value);
        Assert.Equal(45, payload.NewDurationDays);
        Assert.Equal(9, payload.ToRemindAfterMonths);
        Assert.Equal(18, payload.InactiveAfterMonths);
    }
}
