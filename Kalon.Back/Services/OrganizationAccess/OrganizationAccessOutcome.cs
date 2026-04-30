namespace Kalon.Back.Services.OrganizationAccess;

public abstract record OrganizationAccessOutcome
{
    private OrganizationAccessOutcome()
    {
    }

    public sealed record InvalidUserId : OrganizationAccessOutcome;

    public sealed record OrganizationNotFoundForUser : OrganizationAccessOutcome;

    public sealed record Ok(Guid OrganizationId) : OrganizationAccessOutcome;
}
