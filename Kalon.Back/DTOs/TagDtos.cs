namespace Kalon.Back.DTOs;

public class TagResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TagCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}

public class TagUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}
