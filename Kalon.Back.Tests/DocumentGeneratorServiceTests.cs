using Kalon.Back.Models;
using Kalon.Back.Services.Mail;
using QuestPDF.Infrastructure;

namespace Kalon.Back.Tests;

public class DocumentGeneratorServiceTests
{
    private static PrintPageData CreatePage(string html) =>
        new()
        {
            Contact = new Contact
            {
                Kind = ContactKinds.Donor,
                Firstname = "Jane",
                Lastname = "Doe"
            },
            Organization = new Organization
            {
                Name = "Association Kalon",
                Email = "asso@test.local"
            },
            ResolvedHtml = html,
            ResolvedSubject = "Objet test",
            DocumentType = DocumentType.Message
        };

    [Fact]
    public void GenerateSingle_ReturnsPdfBytes()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var service = new DocumentGeneratorService();

        var pdfBytes = service.GenerateSingle(CreatePage("<p>Bonjour<br>test</p>"));

        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.Equal((byte)'%', pdfBytes[0]);
        Assert.Equal((byte)'P', pdfBytes[1]);
        Assert.Equal((byte)'D', pdfBytes[2]);
        Assert.Equal((byte)'F', pdfBytes[3]);
    }

    [Fact]
    public void GenerateMultiPage_ReturnsPdfBytes()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var service = new DocumentGeneratorService();

        var pdfBytes = service.GenerateMultiPage(
        [
            CreatePage("<p>Premier document</p>"),
            CreatePage("<p>Second document</p>")
        ]);

        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.Equal((byte)'%', pdfBytes[0]);
        Assert.Equal((byte)'P', pdfBytes[1]);
        Assert.Equal((byte)'D', pdfBytes[2]);
        Assert.Equal((byte)'F', pdfBytes[3]);
    }
}
