using System.Text.RegularExpressions;
using Kalon.Back.Configuration;
using Kalon.Back.Data;
using Kalon.Back.Dtos;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Kalon.Back.Services.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

public class PasswordResetServiceTests
{
    private const string Pepper = "viser_lindependance_financiere_002";

    private sealed class CapturingMailService : IMailService
    {
        public MailMessageDto? LastMessage { get; private set; }

        public Task SendAsync(MailMessageDto message)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }
    }

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static PasswordService CreatePasswordService()
    {
        return new PasswordService(Options.Create(new PasswordOptions
        {
            Pepper = Pepper,
            Iterations = 120000,
            HashSize = 32
        }));
    }

    private static PasswordResetService CreateService(
        ApplicationDbContext db,
        CapturingMailService mailService,
        IPasswordService? passwordService = null)
    {
        return new PasswordResetService(
            db,
            passwordService ?? CreatePasswordService(),
            mailService,
            Options.Create(new PasswordResetOptions
            {
                FrontendResetUrl = "http://localhost:4300/reset-password",
                TokenExpirationMinutes = 60
            }));
    }

    private static User CreateUser(string email = "john@doe.com")
    {
        var passwordService = CreatePasswordService();
        var salt = passwordService.GenerateSalt();
        return new User
        {
            Id = Guid.NewGuid(),
            MeranId = Guid.NewGuid(),
            Firstname = "John",
            Lastname = "Doe",
            Email = email,
            AssociationName = "Asso",
            Salt = salt,
            PasswordHash = passwordService.HashPassword("OldPassword123!", salt)
        };
    }

    private static string? ExtractTokenFromEmail(string bodyHtml)
    {
        var match = Regex.Match(bodyHtml, @"token=([^""&]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }

    [Fact]
    public async Task RequestResetAsync_SendsEmail_WhenUserExists()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var mailService = new CapturingMailService();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db, mailService);
        await service.RequestResetAsync("john@doe.com", CancellationToken.None);

        Assert.NotNull(mailService.LastMessage);
        Assert.Equal("john@doe.com", mailService.LastMessage.ToEmail);
        Assert.Contains("Réinitialiser mon mot de passe", mailService.LastMessage.BodyHtml);

        var tokenCount = await db.PasswordResetTokens.CountAsync(t => t.UserId == user.Id && t.UsedAt == null);
        Assert.Equal(1, tokenCount);
    }

    [Fact]
    public async Task RequestResetAsync_DoesNotSendEmail_WhenUserMissing()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var mailService = new CapturingMailService();
        var service = CreateService(db, mailService);

        await service.RequestResetAsync("missing@doe.com", CancellationToken.None);

        Assert.Null(mailService.LastMessage);
        Assert.Equal(0, await db.PasswordResetTokens.CountAsync());
    }

    [Fact]
    public async Task RequestResetAsync_InvalidatesPreviousTokens()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var mailService = new CapturingMailService();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db, mailService);
        await service.RequestResetAsync(user.Email, CancellationToken.None);
        await service.RequestResetAsync(user.Email, CancellationToken.None);

        var activeTokens = await db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync();
        Assert.Single(activeTokens);

        var usedTokens = await db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt != null)
            .CountAsync();
        Assert.Equal(1, usedTokens);
    }

    [Fact]
    public async Task ResetPasswordAsync_UpdatesPassword_WhenTokenValid()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var mailService = new CapturingMailService();
        var passwordService = CreatePasswordService();
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = CreateService(db, mailService, passwordService);
        await service.RequestResetAsync(user.Email, CancellationToken.None);

        var token = ExtractTokenFromEmail(mailService.LastMessage!.BodyHtml);
        Assert.NotNull(token);

        var result = await service.ResetPasswordAsync(token, "NewPassword456!", CancellationToken.None);

        Assert.Equal(PasswordResetStatus.Success, result.Status);

        var updated = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.True(passwordService.VerifyPassword("NewPassword456!", updated.PasswordHash, updated.Salt));
        Assert.False(passwordService.VerifyPassword("OldPassword123!", updated.PasswordHash, updated.Salt));

        var tokenEntity = await db.PasswordResetTokens.SingleAsync(t => t.UserId == user.Id);
        Assert.NotNull(tokenEntity.UsedAt);
    }

    [Fact]
    public async Task ResetPasswordAsync_ReturnsInvalid_WhenTokenExpired()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var passwordService = CreatePasswordService();
        var user = CreateUser();
        db.Users.Add(user);

        var rawToken = PasswordResetService.GenerateResetToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = PasswordResetService.HashToken(rawToken),
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new CapturingMailService(), passwordService);
        var result = await service.ResetPasswordAsync(rawToken, "NewPassword456!", CancellationToken.None);

        Assert.Equal(PasswordResetStatus.InvalidOrExpiredToken, result.Status);
    }

    [Fact]
    public async Task ResetPasswordAsync_ReturnsInvalid_WhenTokenAlreadyUsed()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var passwordService = CreatePasswordService();
        var user = CreateUser();
        db.Users.Add(user);

        var rawToken = PasswordResetService.GenerateResetToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = PasswordResetService.HashToken(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            UsedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new CapturingMailService(), passwordService);
        var result = await service.ResetPasswordAsync(rawToken, "NewPassword456!", CancellationToken.None);

        Assert.Equal(PasswordResetStatus.InvalidOrExpiredToken, result.Status);
    }
}
