using System.Security.Claims;
using Kalon.Back.DTOs;
using Kalon.Back.Services;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "organization_master")]
public class TagController(
    ITagService tagService,
    IUserOrganizationAccessService userOrganizationAccess) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<TagResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var access = await ResolveOrganizationAccessAsync(cancellationToken);
        if (!access.Success)
            return access.Error!;

        var tags = await tagService.GetAllAsync(access.OrganizationId, cancellationToken);
        return Ok(tags);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TagResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var access = await ResolveOrganizationAccessAsync(cancellationToken);
        if (!access.Success)
            return access.Error!;

        var tag = await tagService.GetByIdAsync(access.OrganizationId, id, cancellationToken);
        if (tag is null)
            return NotFound(new ApiMessageResponse { Message = "Tag not found." });

        return Ok(tag);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TagResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] TagCreateRequest request,
        CancellationToken cancellationToken)
    {
        var access = await ResolveOrganizationAccessAsync(cancellationToken);
        if (!access.Success)
            return access.Error!;

        var result = await tagService.CreateAsync(access.OrganizationId, request, cancellationToken);
        return result.Status switch
        {
            TagOperationStatus.Success => CreatedAtAction(nameof(GetById), new { id = result.Tag!.Id }, result.Tag),
            TagOperationStatus.Conflict => Conflict(new ApiMessageResponse { Message = result.Message ?? "Conflict." }),
            TagOperationStatus.Invalid => BadRequest(new ApiMessageResponse { Message = result.Message ?? "Invalid request." }),
            _ => BadRequest(new ApiMessageResponse { Message = result.Message ?? "Unable to create tag." })
        };
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TagResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] TagUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var access = await ResolveOrganizationAccessAsync(cancellationToken);
        if (!access.Success)
            return access.Error!;

        var result = await tagService.UpdateAsync(access.OrganizationId, id, request, cancellationToken);
        return result.Status switch
        {
            TagOperationStatus.Success => Ok(result.Tag),
            TagOperationStatus.NotFound => NotFound(new ApiMessageResponse { Message = result.Message ?? "Tag not found." }),
            TagOperationStatus.Conflict => Conflict(new ApiMessageResponse { Message = result.Message ?? "Conflict." }),
            TagOperationStatus.Invalid => BadRequest(new ApiMessageResponse { Message = result.Message ?? "Invalid request." }),
            _ => BadRequest(new ApiMessageResponse { Message = result.Message ?? "Unable to update tag." })
        };
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var access = await ResolveOrganizationAccessAsync(cancellationToken);
        if (!access.Success)
            return access.Error!;

        var result = await tagService.DeleteAsync(access.OrganizationId, id, cancellationToken);
        return result.Status switch
        {
            TagOperationStatus.Success => NoContent(),
            TagOperationStatus.NotFound => NotFound(new ApiMessageResponse { Message = result.Message ?? "Tag not found." }),
            _ => BadRequest(new ApiMessageResponse { Message = result.Message ?? "Unable to delete tag." })
        };
    }

    private async Task<(bool Success, Guid OrganizationId, IActionResult? Error)> ResolveOrganizationAccessAsync(
        CancellationToken cancellationToken)
    {
        var userId = ResolveUserIdFromJwt();
        if (userId is null)
            return (false, Guid.Empty, BadRequest(new ApiMessageResponse { Message = "userId is required." }));

        var access = await userOrganizationAccess.ResolveAsync(userId.Value, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return (false, Guid.Empty, resolved.Error);

        return (true, resolved.OrganizationId, null);
    }

    private Guid? ResolveUserIdFromJwt()
    {
        var principal = HttpContext?.User;
        if (principal is null)
            return null;

        var claimValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? principal.FindFirstValue("sub");
        return Guid.TryParse(claimValue, out var parsed) ? parsed : null;
    }
}
