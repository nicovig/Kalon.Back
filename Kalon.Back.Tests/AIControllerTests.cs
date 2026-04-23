using System.Security.Claims;
using Kalon.Back.Controllers;
using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Tests;

public class AIControllerTests
{
    private sealed class FakeAiMailGeneratorService : IAiMailGeneratorService
    {
        public bool ThrowUnavailable { get; set; }

        public Task<AiMailResultDto> GenerateAsync(AiMailRequestDto request, Organization org)
        {
            if (ThrowUnavailable)
                throw new InvalidOperationException("Anthropic API key missing.");

            return Task.FromResult(new AiMailResultDto
            {
                Subject = "Sujet IA",
                BodyHtml = "<p>Corps IA</p>"
            });
        }
    }

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AIMailController CreateController(
        ApplicationDbContext db,
        IAiMailGeneratorService aiService,
        Guid organizationId)
    {
        var controller = new AIMailController(db, aiService);
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

    [Fact]
    public async Task GenerateMail_ReturnsBadRequest_WhenUserContextMissing()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var controller = CreateController(db, new FakeAiMailGeneratorService(), Guid.NewGuid());

        var result = await controller.GenerateMail(new AiMailRequestDto
        {
            UserContext = "",
            EmailType = EmailTemplateTypes.Reminder
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<ApiMessageResponse>(badRequest.Value);
        Assert.Equal("Le contexte est requis.", payload.Message);
    }

    [Fact]
    public async Task GenerateMail_ReturnsBadRequest_WhenEmailTypeInvalid()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var controller = CreateController(db, new FakeAiMailGeneratorService(), Guid.NewGuid());

        var result = await controller.GenerateMail(new AiMailRequestDto
        {
            UserContext = "Contexte",
            EmailType = "invalid"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<ApiMessageResponse>(badRequest.Value);
        Assert.Equal("Type de mail invalide.", payload.Message);
    }

    [Fact]
    public async Task GenerateMail_ReturnsNotFound_WhenOrganizationMissing()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var controller = CreateController(db, new FakeAiMailGeneratorService(), Guid.NewGuid());

        var result = await controller.GenerateMail(new AiMailRequestDto
        {
            UserContext = "Contexte",
            EmailType = EmailTemplateTypes.Reminder
        });

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var payload = Assert.IsType<ApiMessageResponse>(notFound.Value);
        Assert.Equal("Organisation introuvable.", payload.Message);
    }

    [Fact]
    public async Task GenerateMail_ReturnsServiceUnavailable_WhenAiFails()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Asso",
            Email = "contact@asso.org",
            UserId = Guid.NewGuid(),
            User = new User
            {
                Id = Guid.NewGuid(),
                MeranId = Guid.NewGuid(),
                Firstname = "Owner",
                Lastname = "User",
                Email = "owner@asso.org",
                AssociationName = "Asso",
                PasswordHash = "hash",
                Salt = "salt"
            },
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, new FakeAiMailGeneratorService { ThrowUnavailable = true }, organizationId);

        var result = await controller.GenerateMail(new AiMailRequestDto
        {
            UserContext = "Contexte",
            EmailType = EmailTemplateTypes.Reminder
        });

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var payload = Assert.IsType<ApiMessageResponse>(objectResult.Value);
        Assert.Equal("Anthropic API key missing.", payload.Message);
    }

    [Fact]
    public async Task GenerateMail_ReturnsOk_WhenRequestValid()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Asso",
            Email = "contact@asso.org",
            UserId = Guid.NewGuid(),
            User = new User
            {
                Id = Guid.NewGuid(),
                MeranId = Guid.NewGuid(),
                Firstname = "Owner",
                Lastname = "User",
                Email = "owner@asso.org",
                AssociationName = "Asso",
                PasswordHash = "hash",
                Salt = "salt"
            },
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, new FakeAiMailGeneratorService(), organizationId);

        var result = await controller.GenerateMail(new AiMailRequestDto
        {
            UserContext = "Contexte",
            EmailType = EmailTemplateTypes.Reminder
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AiMailResultDto>(ok.Value);
        Assert.Equal("Sujet IA", payload.Subject);
        Assert.Equal("<p>Corps IA</p>", payload.BodyHtml);
    }
}
