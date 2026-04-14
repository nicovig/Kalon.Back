namespace Kalon.Back.DTOs;

public class ApiMessageResponse
{
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
