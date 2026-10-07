using Kalon.Back.Data;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Microsoft.EntityFrameworkCore;

namespace Kalon.Back.Services;

public enum TagOperationStatus
{
    Success,
    NotFound,
    Conflict,
    Invalid
}

public sealed record TagOperationResult(TagOperationStatus Status, TagResponse? Tag = null, string? Message = null);

public interface ITagService
{
    Task<IReadOnlyList<TagResponse>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<TagResponse?> GetByIdAsync(Guid organizationId, Guid tagId, CancellationToken cancellationToken);

    Task<TagOperationResult> CreateAsync(
        Guid organizationId,
        TagCreateRequest request,
        CancellationToken cancellationToken);

    Task<TagOperationResult> UpdateAsync(
        Guid organizationId,
        Guid tagId,
        TagUpdateRequest request,
        CancellationToken cancellationToken);

    Task<TagOperationResult> DeleteAsync(
        Guid organizationId,
        Guid tagId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Tag>> ResolveTagsForOrganizationAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid>? tagIds,
        CancellationToken cancellationToken);
}

public class TagService(ApplicationDbContext dbContext) : ITagService
{
    public async Task<IReadOnlyList<TagResponse>> GetAllAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Tags
            .AsNoTracking()
            .Where(t => t.OrganizationId == organizationId)
            .OrderBy(t => t.Name)
            .Select(t => new TagResponse
            {
                Id = t.Id,
                OrganizationId = t.OrganizationId,
                Name = t.Name,
                Color = t.Color,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TagResponse?> GetByIdAsync(
        Guid organizationId,
        Guid tagId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Tags
            .AsNoTracking()
            .Where(t => t.OrganizationId == organizationId && t.Id == tagId)
            .Select(t => new TagResponse
            {
                Id = t.Id,
                OrganizationId = t.OrganizationId,
                Name = t.Name,
                Color = t.Color,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TagOperationResult> CreateAsync(
        Guid organizationId,
        TagCreateRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateName(request.Name);
        if (validationError is not null)
            return new TagOperationResult(TagOperationStatus.Invalid, Message: validationError);

        var name = request.Name.Trim();
        var exists = await dbContext.Tags.AnyAsync(
            t => t.OrganizationId == organizationId && t.Name.ToLower() == name.ToLower(),
            cancellationToken);

        if (exists)
            return new TagOperationResult(
                TagOperationStatus.Conflict,
                Message: "A tag with this name already exists.");

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Color = NormalizeColor(request.Color),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TagOperationResult(TagOperationStatus.Success, ToResponse(tag));
    }

    public async Task<TagOperationResult> UpdateAsync(
        Guid organizationId,
        Guid tagId,
        TagUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateName(request.Name);
        if (validationError is not null)
            return new TagOperationResult(TagOperationStatus.Invalid, Message: validationError);

        var tag = await dbContext.Tags
            .FirstOrDefaultAsync(t => t.OrganizationId == organizationId && t.Id == tagId, cancellationToken);

        if (tag is null)
            return new TagOperationResult(TagOperationStatus.NotFound, Message: "Tag not found.");

        var name = request.Name.Trim();
        var exists = await dbContext.Tags.AnyAsync(
            t => t.OrganizationId == organizationId
                 && t.Id != tagId
                 && t.Name.ToLower() == name.ToLower(),
            cancellationToken);

        if (exists)
            return new TagOperationResult(
                TagOperationStatus.Conflict,
                Message: "A tag with this name already exists.");

        tag.Name = name;
        tag.Color = NormalizeColor(request.Color);
        tag.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TagOperationResult(TagOperationStatus.Success, ToResponse(tag));
    }

    public async Task<TagOperationResult> DeleteAsync(
        Guid organizationId,
        Guid tagId,
        CancellationToken cancellationToken)
    {
        var tag = await dbContext.Tags
            .Include(t => t.Contacts)
            .FirstOrDefaultAsync(t => t.OrganizationId == organizationId && t.Id == tagId, cancellationToken);

        if (tag is null)
            return new TagOperationResult(TagOperationStatus.NotFound, Message: "Tag not found.");

        tag.Contacts.Clear();
        dbContext.Tags.Remove(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TagOperationResult(TagOperationStatus.Success);
    }

    public async Task<IReadOnlyList<Tag>> ResolveTagsForOrganizationAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid>? tagIds,
        CancellationToken cancellationToken)
    {
        if (tagIds is null || tagIds.Count == 0)
            return [];

        var distinctIds = tagIds.Distinct().ToList();
        var tags = await dbContext.Tags
            .Where(t => t.OrganizationId == organizationId && distinctIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        return tags;
    }

    private static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Name is required.";
        if (name.Trim().Length > 100)
            return "Name must be at most 100 characters.";
        return null;
    }

    private static string? NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return null;
        return color.Trim();
    }

    private static TagResponse ToResponse(Tag tag) => new()
    {
        Id = tag.Id,
        OrganizationId = tag.OrganizationId,
        Name = tag.Name,
        Color = tag.Color,
        CreatedAt = tag.CreatedAt,
        UpdatedAt = tag.UpdatedAt
    };
}
