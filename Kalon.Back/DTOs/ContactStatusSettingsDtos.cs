namespace Kalon.Back.DTOs;

public class ContactStatusSettingsUpsertRequest
{
    public Guid? Id { get; set; }
    public int NewDurationDays { get; set; }
    public int ToRemindAfterMonths { get; set; }
    public int InactiveAfterMonths { get; set; }
}
