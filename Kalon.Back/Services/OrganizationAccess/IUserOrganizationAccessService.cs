namespace Kalon.Back.Services.OrganizationAccess;

public interface IUserOrganizationAccessService
{
    Task<OrganizationAccessOutcome> ResolveAsync(Guid userId, CancellationToken cancellationToken = default);
}
