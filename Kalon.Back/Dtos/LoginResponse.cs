namespace Kalon.Back.Dtos;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public LoginUserResponse User { get; set; } = new();
    public MeranMembershipStatusResponse Meran { get; set; } = new();
}

public class LoginUserResponse
{
    public Guid Id { get; set; }
    public string Firstname { get; set; } = string.Empty;
    public string Lastname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AssociationName { get; set; } = string.Empty;
    public Guid MeranId { get; set; }
}
