using Kalon.Back.Data;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Services;

public enum PasswordChangeStatus
{
    Success,
    UserNotFound,
    InvalidCurrentPassword
}

public sealed record PasswordChangeResult(PasswordChangeStatus Status);

public interface IUserPasswordService
{
    Task<PasswordChangeResult> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);
}

public class UserPasswordService(
    ApplicationDbContext dbContext,
    IPasswordService passwordService) : IUserPasswordService
{
    public async Task<PasswordChangeResult> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return new PasswordChangeResult(PasswordChangeStatus.UserNotFound);

        if (!passwordService.VerifyPassword(currentPassword, user.PasswordHash, user.Salt))
            return new PasswordChangeResult(PasswordChangeStatus.InvalidCurrentPassword);

        var newSalt = passwordService.GenerateSalt();
        user.Salt = newSalt;
        user.PasswordHash = passwordService.HashPassword(newPassword, newSalt);

        await dbContext.SaveChangesAsync(cancellationToken);
        return new PasswordChangeResult(PasswordChangeStatus.Success);
    }
}
