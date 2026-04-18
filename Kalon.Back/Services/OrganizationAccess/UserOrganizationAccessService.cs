using Kalon.Back.Data;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Services.OrganizationAccess;

public interface IUserOrganizationAccessService
{
    Task<OrganizationAccessOutcome> ResolveAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class UserOrganizationAccessService(ApplicationDbContext dbContext) : IUserOrganizationAccessService
{
    public async Task<OrganizationAccessOutcome> ResolveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return new OrganizationAccessOutcome.InvalidUserId();

        var organizationId = await dbContext.Organizations
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (organizationId is null)
            return new OrganizationAccessOutcome.OrganizationNotFoundForUser();

        return new OrganizationAccessOutcome.Ok(organizationId.Value);
    }
}
