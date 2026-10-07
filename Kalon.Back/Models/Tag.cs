using System.ComponentModel.DataAnnotations;

namespace Kalon.Back.Models;

public class Tag
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid OrganizationId { get; set; }

    public Organization Organization { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? Color { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
}
