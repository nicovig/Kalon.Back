using System.ComponentModel.DataAnnotations;

namespace Kalon.Back.Models;

public class User
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid MeranId { get; set; }

    [Required]
    public string Firstname { get; set; } = string.Empty;

    [Required]
    public string Lastname { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string AssociationName { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public string Salt { get; set; } = string.Empty;

}
