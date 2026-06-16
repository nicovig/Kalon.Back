using Kalon.Back.Data;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

public class UserPasswordServiceTests
{
    private const string Pepper = "viser_lindependance_financiere_002";

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

    private static User CreateUserWithPassword(PasswordService passwordService, string password)
    {
        var salt = passwordService.GenerateSalt();
        return new User
        {
            Id = Guid.NewGuid(),
            MeranId = Guid.NewGuid(),
            Firstname = "John",
            Lastname = "Doe",
            Email = "john@doe.com",
            AssociationName = "Asso",
            Salt = salt,
            PasswordHash = passwordService.HashPassword(password, salt)
        };
    }

    [Fact]
    public async Task ChangePasswordAsync_UpdatesHashAndSalt_WhenCurrentPasswordValid()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var passwordService = CreatePasswordService();
        var user = CreateUserWithPassword(passwordService, "OldPassword123!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var oldSalt = user.Salt;
        var oldHash = user.PasswordHash;

        var service = new UserPasswordService(db, passwordService);
        var result = await service.ChangePasswordAsync(
            user.Id,
            "OldPassword123!",
            "NewPassword456!",
            CancellationToken.None);

        Assert.Equal(PasswordChangeStatus.Success, result.Status);

        var updated = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.NotEqual(oldSalt, updated.Salt);
        Assert.NotEqual(oldHash, updated.PasswordHash);
        Assert.False(passwordService.VerifyPassword("OldPassword123!", updated.PasswordHash, updated.Salt));
        Assert.True(passwordService.VerifyPassword("NewPassword456!", updated.PasswordHash, updated.Salt));
    }

    [Fact]
    public async Task ChangePasswordAsync_ReturnsInvalidCurrentPassword_WhenWrongPassword()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var passwordService = CreatePasswordService();
        var user = CreateUserWithPassword(passwordService, "OldPassword123!");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var originalSalt = user.Salt;
        var originalHash = user.PasswordHash;

        var service = new UserPasswordService(db, passwordService);
        var result = await service.ChangePasswordAsync(
            user.Id,
            "WrongPassword!",
            "NewPassword456!",
            CancellationToken.None);

        Assert.Equal(PasswordChangeStatus.InvalidCurrentPassword, result.Status);

        var unchanged = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(originalSalt, unchanged.Salt);
        Assert.Equal(originalHash, unchanged.PasswordHash);
    }

    [Fact]
    public async Task ChangePasswordAsync_ReturnsUserNotFound_WhenMissing()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new UserPasswordService(db, CreatePasswordService());

        var result = await service.ChangePasswordAsync(
            Guid.NewGuid(),
            "OldPassword123!",
            "NewPassword456!",
            CancellationToken.None);

        Assert.Equal(PasswordChangeStatus.UserNotFound, result.Status);
    }
}
