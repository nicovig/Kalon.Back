using System.ComponentModel.DataAnnotations;

namespace Kalon.Back.Models;

public class PasswordResetToken
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    [Required]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }
}
