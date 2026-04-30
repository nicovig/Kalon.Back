namespace Kalon.Back.Dtos.Errors;

public class QuotaExceededException : Exception
{
    public string QuotaType { get; }
    public int Current { get; }
    public int Limit { get; }

    public QuotaExceededException(string quotaType, int current, int limit)
        : base($"Quota '{quotaType}' dépassé ({current}/{limit}).")
    {
        QuotaType = quotaType;
        Current = current;
        Limit = limit;
    }
}