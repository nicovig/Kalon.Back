using System.Text;
using Kalon.Back.Services.Mail;
using Microsoft.AspNetCore.Http;

namespace Kalon.Back.Tests;

public class SendingEmailAttachmentsTests
{
    private static IFormFile CreateFormFile(string fileName, string content, string contentType = "application/pdf")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "attachments", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task ParseAsync_ReturnsEmpty_WhenNoFiles()
    {
        var (attachments, error) = await SendingEmailAttachments.ParseAsync(null);

        Assert.Null(error);
        Assert.Empty(attachments);
    }

    [Fact]
    public async Task ParseAsync_ReturnsError_WhenMoreThanTwoFiles()
    {
        var files = new List<IFormFile>
        {
            CreateFormFile("a.pdf", "a"),
            CreateFormFile("b.pdf", "b"),
            CreateFormFile("c.pdf", "c")
        };

        var (_, error) = await SendingEmailAttachments.ParseAsync(files);

        Assert.Equal("Maximum 2 attachments allowed.", error);
    }

    [Fact]
    public async Task ParseAsync_ReturnsError_WhenExtensionNotAllowed()
    {
        var files = new List<IFormFile> { CreateFormFile("virus.exe", "bad") };

        var (_, error) = await SendingEmailAttachments.ParseAsync(files);

        Assert.Equal("Attachment type '.exe' is not allowed.", error);
    }

    [Fact]
    public async Task ParseAsync_ReturnsAttachments_WhenValid()
    {
        var files = new List<IFormFile>
        {
            CreateFormFile("brochure.pdf", "pdf-content"),
            CreateFormFile("photo.jpg", "jpg-content", "image/jpeg")
        };

        var (attachments, error) = await SendingEmailAttachments.ParseAsync(files);

        Assert.Null(error);
        Assert.Equal(2, attachments.Count);
        Assert.Equal("brochure.pdf", attachments[0].FileName);
        Assert.Equal("application/pdf", attachments[0].ContentType);
        Assert.Equal("photo.jpg", attachments[1].FileName);
        Assert.Equal("image/jpeg", attachments[1].ContentType);
    }
}
