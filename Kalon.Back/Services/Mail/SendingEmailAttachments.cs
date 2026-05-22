using Kalon.Back.Dtos;
using Microsoft.AspNetCore.Http;

namespace Kalon.Back.Services.Mail;

public static class SendingEmailAttachments
{
    public const int MaxCount = 2;
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg"
    };

    private static readonly Dictionary<string, string> ExtensionToContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg"
    };

    public static async Task<(IReadOnlyList<EmailAttachmentDto> Attachments, string? Error)> ParseAsync(
        IReadOnlyList<IFormFile>? files,
        CancellationToken cancellationToken = default)
    {
        if (files is null || files.Count == 0)
            return ([], null);

        var nonEmpty = files.Where(f => f.Length > 0).ToList();
        if (nonEmpty.Count == 0)
            return ([], null);

        if (nonEmpty.Count > MaxCount)
            return ([], $"Maximum {MaxCount} attachments allowed.");

        var result = new List<EmailAttachmentDto>(nonEmpty.Count);

        foreach (var file in nonEmpty)
        {
            if (file.Length > MaxFileSizeBytes)
                return ([], $"Attachment '{file.FileName}' exceeds maximum size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

            var fileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(fileName))
                return ([], "Attachment file name is required.");

            var extension = Path.GetExtension(fileName);
            if (!AllowedExtensions.Contains(extension))
                return ([], $"Attachment type '{extension}' is not allowed.");

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);

            result.Add(new EmailAttachmentDto
            {
                FileName = fileName,
                Content = stream.ToArray(),
                ContentType = ResolveContentType(file.ContentType, extension)
            });
        }

        return (result, null);
    }

    private static string ResolveContentType(string? uploadedContentType, string extension)
    {
        if (!string.IsNullOrWhiteSpace(uploadedContentType)
            && uploadedContentType != "application/octet-stream")
            return uploadedContentType;

        return ExtensionToContentType.GetValueOrDefault(extension, "application/octet-stream");
    }
}
