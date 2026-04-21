using System.ComponentModel.DataAnnotations;

namespace Kalon.Back.DTOs;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public LoginUserResponse User { get; set; } = new();
    public MeranMembershipStatus Meran { get; set; } = new();
}

public class LoginUserResponse
{
    public Guid Id { get; set; }
    public string Firstname { get; set; } = string.Empty;
    public string Lastname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid MeranId { get; set; }
    public OrganizationLoginResponse Organization { get; set; } = new();
}

public class OrganizationLoginResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FiscalStatus { get; set; } = string.Empty;
    public ContactStatusSettingsSummary? ContactStatusSettings { get; set; }
}

public class ContactStatusSettingsSummary
{
    public int NewDurationDays { get; set; }
    public int ToRemindAfterMonths { get; set; }
    public int InactiveAfterMonths { get; set; }
}

public class MeranMembershipStatus
{
    public bool IsActive { get; set; }
    public string? Plan { get; set; }
}
