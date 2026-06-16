using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Kalon.Back.Configuration;
using Kalon.Back.Data;
using Kalon.Back.Dtos;
using Kalon.Back.Services.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Services;

public enum PasswordResetStatus
{
    Success,
    InvalidOrExpiredToken
}

public sealed record PasswordResetResult(PasswordResetStatus Status);

public interface IPasswordResetService
{
    Task RequestResetAsync(string email, CancellationToken cancellationToken);

    Task<PasswordResetResult> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken);
}

public class PasswordResetService(
    ApplicationDbContext dbContext,
    IPasswordService passwordService,
    IMailService mailService,
    IOptions<PasswordResetOptions> options) : IPasswordResetService
{
    private readonly PasswordResetOptions _options = options.Value;

    public async Task RequestResetAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
            return;

        var now = DateTime.UtcNow;
        var existingTokens = await dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingTokens)
            existing.UsedAt = now;

        var rawToken = GenerateResetToken();
        var tokenEntity = new Models.PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(_options.TokenExpirationMinutes)
        };

        dbContext.PasswordResetTokens.Add(tokenEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var resetUrl = BuildResetUrl(rawToken);
        var displayName = $"{user.Firstname} {user.Lastname}".Trim();

        await mailService.SendAsync(new MailMessageDto
        {
            ToEmail = user.Email,
            ToName = string.IsNullOrWhiteSpace(displayName) ? user.Email : displayName,
            Subject = "Réinitialisation de votre mot de passe Kalon",
            BodyHtml = BuildResetEmailHtml(displayName, resetUrl, _options.TokenExpirationMinutes)
        });
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            return new PasswordResetResult(PasswordResetStatus.InvalidOrExpiredToken);

        var tokenHash = HashToken(token.Trim());
        var now = DateTime.UtcNow;

        var tokenEntity = await dbContext.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash
                     && t.UsedAt == null
                     && t.ExpiresAt > now,
                cancellationToken);

        if (tokenEntity?.User is null)
            return new PasswordResetResult(PasswordResetStatus.InvalidOrExpiredToken);

        var user = tokenEntity.User;
        var newSalt = passwordService.GenerateSalt();
        user.Salt = newSalt;
        user.PasswordHash = passwordService.HashPassword(newPassword, newSalt);
        tokenEntity.UsedAt = now;

        var otherActiveTokens = await dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.Id != tokenEntity.Id)
            .ToListAsync(cancellationToken);

        foreach (var other in otherActiveTokens)
            other.UsedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new PasswordResetResult(PasswordResetStatus.Success);
    }

    public static string GenerateResetToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }

    private string BuildResetUrl(string rawToken)
    {
        var baseUrl = _options.FrontendResetUrl.TrimEnd('/');
        return $"{baseUrl}?token={Uri.EscapeDataString(rawToken)}";
    }

    private static string BuildResetEmailHtml(string displayName, string resetUrl, int expirationMinutes)
    {
        var greeting = string.IsNullOrWhiteSpace(displayName)
            ? "Bonjour,"
            : $"Bonjour {EncodeHtml(displayName)},";

        return $"""
                <p>{greeting}</p>
                <p>Vous avez demandé la réinitialisation de votre mot de passe Kalon.</p>
                <p><a href="{EncodeHtml(resetUrl)}">Réinitialiser mon mot de passe</a></p>
                <p>Ce lien expire dans {expirationMinutes} minutes. Si vous n'êtes pas à l'origine de cette demande, ignorez cet e-mail.</p>
                """;
    }

    private static string EncodeHtml(string value) =>
        Regex.Replace(value, "[&<>\"']", match => match.Value switch
        {
            "&" => "&amp;",
            "<" => "&lt;",
            ">" => "&gt;",
            "\"" => "&quot;",
            "'" => "&#39;",
            _ => match.Value
        });
}
