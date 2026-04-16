using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.OrganizationAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganizationCustomContentController(
    ApplicationDbContext dbContext,
    IUserOrganizationAccessService userOrganizationAccess) : ControllerBase
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "text",
        "image",
        "signature"
    };

    [HttpPost("content-blocks")]
    [ProducesResponseType(typeof(ContentBlockResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateContentBlock([FromQuery] Guid userId, [FromBody] ContentBlockUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return BadRequest(new ApiMessageResponse { Message = validationError });

        var block = new ContentBlock
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedAt = DateTime.UtcNow
        };
        ApplyRequest(block, request);

        dbContext.ContentBlocks.Add(block);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await ProjectAsync(block.Id, organizationId, cancellationToken);
        return CreatedAtAction(nameof(GetContentBlockById), new { userId, id = block.Id }, result);
    }

    [HttpGet("content-blocks")]
    [ProducesResponseType(typeof(List<ContentBlockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContentBlocks([FromQuery] Guid userId, CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;

        var items = await dbContext.ContentBlocks
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ContentBlockResponse
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                Name = x.Name,
                Kind = x.Kind,
                Content = x.Content,
                StoredPath = x.StoredPath,
                MimeType = x.MimeType,
                UsableInEmail = x.UsableInEmail,
                UsableInReceipt = x.UsableInReceipt,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("content-blocks/{id:guid}")]
    [ProducesResponseType(typeof(ContentBlockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContentBlockById([FromQuery] Guid userId, [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var result = await ProjectAsync(id, organizationId, cancellationToken);
        if (result is null)
            return NotFound(new ApiMessageResponse { Message = "Content block not found." });

        return Ok(result);
    }

    [HttpPut("content-blocks/{id:guid}")]
    [ProducesResponseType(typeof(ContentBlockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateContentBlock([FromQuery] Guid userId, [FromRoute] Guid id,
        [FromBody] ContentBlockUpsertRequest request, CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return BadRequest(new ApiMessageResponse { Message = validationError });

        var block = await dbContext.ContentBlocks
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);
        if (block is null)
            return NotFound(new ApiMessageResponse { Message = "Content block not found." });

        ApplyRequest(block, request);
        block.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await ProjectAsync(block.Id, organizationId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("content-blocks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteContentBlock([FromQuery] Guid userId, [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var block = await dbContext.ContentBlocks
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);
        if (block is null)
            return NotFound(new ApiMessageResponse { Message = "Content block not found." });

        dbContext.ContentBlocks.Remove(block);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("logo")]
    [ProducesResponseType(typeof(OrganizationLogoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrganizationLogo([FromQuery] Guid userId, CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var logo = await ProjectLogoAsync(organizationId, cancellationToken);
        if (logo is null)
            return NotFound(new ApiMessageResponse { Message = "Organization logo not found." });

        return Ok(logo);
    }

    [HttpPost("logo")]
    [ProducesResponseType(typeof(OrganizationLogoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateOrganizationLogo([FromQuery] Guid userId,
        [FromBody] OrganizationLogoUpsertRequest request, CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var validationError = ValidateLogoRequest(request);
        if (validationError is not null)
            return BadRequest(new ApiMessageResponse { Message = validationError });

        var existing = await dbContext.OrganizationLogos
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (existing is not null)
            return BadRequest(new ApiMessageResponse { Message = "Organization logo already exists. Use PUT to replace it." });

        var logo = new OrganizationLogo
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedAt = DateTime.UtcNow
        };
        ApplyLogoRequest(logo, request);

        dbContext.OrganizationLogos.Add(logo);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await ProjectLogoAsync(organizationId, cancellationToken);
        return CreatedAtAction(nameof(GetOrganizationLogo), new { userId }, result);
    }

    [HttpPut("logo")]
    [ProducesResponseType(typeof(OrganizationLogoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrganizationLogo([FromQuery] Guid userId,
        [FromBody] OrganizationLogoUpsertRequest request, CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var validationError = ValidateLogoRequest(request);
        if (validationError is not null)
            return BadRequest(new ApiMessageResponse { Message = validationError });

        var logo = await dbContext.OrganizationLogos
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (logo is null)
            return NotFound(new ApiMessageResponse { Message = "Organization logo not found." });

        ApplyLogoRequest(logo, request);
        logo.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await ProjectLogoAsync(organizationId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("logo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiMessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOrganizationLogo([FromQuery] Guid userId, CancellationToken cancellationToken)
    {
        var access = await userOrganizationAccess.ResolveAsync(userId, cancellationToken);
        var resolved = access.ToActionResult();
        if (!resolved.Success)
            return resolved.Error!;

        var organizationId = resolved.OrganizationId;
        var logo = await dbContext.OrganizationLogos
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (logo is null)
            return NotFound(new ApiMessageResponse { Message = "Organization logo not found." });

        dbContext.OrganizationLogos.Remove(logo);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? ValidateRequest(ContentBlockUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Name is required.";
        if (!AllowedKinds.Contains(request.Kind))
            return "Invalid kind.";

        var kind = request.Kind.Trim().ToLowerInvariant();
        if (kind == "text" && string.IsNullOrWhiteSpace(request.Content))
            return "Content is required for text kind.";
        if ((kind == "image" || kind == "signature") && string.IsNullOrWhiteSpace(request.StoredPath))
            return "StoredPath is required for image and signature kinds.";
        if ((kind == "image" || kind == "signature") && string.IsNullOrWhiteSpace(request.MimeType))
            return "MimeType is required for image and signature kinds.";

        return null;
    }

    private static void ApplyRequest(ContentBlock block, ContentBlockUpsertRequest request)
    {
        block.Name = request.Name.Trim();
        block.Kind = request.Kind.Trim().ToLowerInvariant();
        block.Content = request.Content?.Trim();
        block.StoredPath = request.StoredPath?.Trim();
        block.MimeType = request.MimeType?.Trim();
        block.UsableInEmail = request.UsableInEmail;
        block.UsableInReceipt = request.UsableInReceipt;
    }

    private static string? ValidateLogoRequest(OrganizationLogoUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            return "FileName is required.";
        if (string.IsNullOrWhiteSpace(request.StoredPath))
            return "StoredPath is required.";
        if (string.IsNullOrWhiteSpace(request.MimeType))
            return "MimeType is required.";
        if (request.FileSizeBytes <= 0)
            return "FileSizeBytes must be greater than 0.";
        return null;
    }

    private static void ApplyLogoRequest(OrganizationLogo logo, OrganizationLogoUpsertRequest request)
    {
        logo.FileName = request.FileName.Trim();
        logo.StoredPath = request.StoredPath.Trim();
        logo.MimeType = request.MimeType.Trim();
        logo.FileSizeBytes = request.FileSizeBytes;
    }

    private Task<ContentBlockResponse?> ProjectAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        return dbContext.ContentBlocks
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Id == id)
            .Select(x => new ContentBlockResponse
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                Name = x.Name,
                Kind = x.Kind,
                Content = x.Content,
                StoredPath = x.StoredPath,
                MimeType = x.MimeType,
                UsableInEmail = x.UsableInEmail,
                UsableInReceipt = x.UsableInReceipt,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<OrganizationLogoResponse?> ProjectLogoAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return dbContext.OrganizationLogos
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => new OrganizationLogoResponse
            {
                Id = x.Id,
                OrganizationId = x.OrganizationId,
                FileName = x.FileName,
                StoredPath = x.StoredPath,
                MimeType = x.MimeType,
                FileSizeBytes = x.FileSizeBytes,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
