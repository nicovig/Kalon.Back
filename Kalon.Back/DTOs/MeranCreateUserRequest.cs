namespace Kalon.Back.DTOs;

public class MeranCreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Plan { get; set; }
}
