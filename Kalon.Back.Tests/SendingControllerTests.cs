using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Kalon.Back.Controllers;
using Kalon.Back.Configuration;
using Kalon.Back.Dtos;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services;
using Kalon.Back.Services.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

public class SendingControllerTests
{
    private sealed class FakeQuotaService : IQuotaService
    {
        public Task CheckAndIncrementAsync(Guid organizationId, string quotaType, int? limit, int increment = 1) => Task.CompletedTask;
        public Task<int> GetCurrentCountAsync(Guid organizationId, string quotaType) => Task.FromResult(0);
    }

    private sealed class FakeSendingService : ISendingService
    {
        public bool ThrowOnPrint { get; set; }
        public bool ThrowOnConfirm { get; set; }
        public bool ThrowOnSend { get; set; }
        public Guid? LastConfirmedMailLogId { get; private set; }
        public IReadOnlyList<EmailAttachmentDto>? LastUserAttachments { get; private set; }

        public Task<SendDocumentResultDto> SendByEmailAsync(
            SendDocumentDto dto,
            Guid organizationId,
            IReadOnlyList<EmailAttachmentDto>? userAttachments = null)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("Association introuvable.");

            LastUserAttachments = userAttachments;

            return Task.FromResult(new SendDocumentResultDto
            {
                SuccessCount = 1,
                ErrorCount = 0
            });
        }

        public Task<PrintDocumentResultDto> GeneratePrintPdfAsync(SendDocumentDto dto, Guid organizationId)
        {
            if (ThrowOnPrint)
                throw new InvalidOperationException("Association introuvable.");

            return Task.FromResult(new PrintDocumentResultDto
            {
                PdfBytes = [0x25, 0x50, 0x44, 0x46],
                PageCount = 1
            });
        }

        public Task ConfirmMailedAsync(Guid mailLogId, Guid organizationId)
        {
            if (ThrowOnConfirm)
                throw new InvalidOperationException("Courrier introuvable.");

            LastConfirmedMailLogId = mailLogId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVariableResolverService : IVariableResolverService
    {
        public string Resolve(string template, Contact contact, Organization org) => template;

        public IReadOnlyList<MailEditorVariableTag> GetAvailableTags(bool hasCompanyRecipient)
            => MailEditorVariableTagCatalog.Get(hasCompanyRecipient);
    }

    private static SendingController CreateController(FakeSendingService service, Guid organizationId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("organization_id", organizationId.ToString()),
                new Claim("plan_features", "{\"max_annual_documents\":\"100\",\"max_annual_emails\":\"100\"}")
            ], "TestAuth"))
        };

        var controller = new SendingController(
            service,
            new FakeVariableResolverService(),
            new FakeQuotaService(),
            new PlanService(
                new HttpContextAccessor { HttpContext = httpContext },
                Options.Create(new PlanOptions
                {
                    MaxDocumentsApplicationFeatureValue = "max_annual_documents",
                    MaxEmailsApplicationFeatureValue = "max_annual_emails"
                })));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        return controller;
    }

    [Fact]
    public void GetMailEditorTags_ReturnsBaseTags_WhenNoCompanyRecipient()
    {
        var controller = CreateController(new FakeSendingService(), Guid.NewGuid());

        var result = controller.GetMailEditorTags(false);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<List<MailEditorVariableTag>>(ok.Value);
        Assert.DoesNotContain(payload, t => t.Id == "enterprise_name");
        Assert.Contains(payload, t => t.Id == "totalDonation");
    }

    [Fact]
    public void GetMailEditorTags_IncludesCompanyTag_WhenCompanyRecipientPresent()
    {
        var controller = CreateController(new FakeSendingService(), Guid.NewGuid());

        var result = controller.GetMailEditorTags(true);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<List<MailEditorVariableTag>>(ok.Value);
        Assert.Contains(payload, t => t.Id == "enterprise_name");
    }

    [Fact]
    public async Task Print_ReturnsBadRequest_WhenDocumentTypeInvalid()
    {
        var service = new FakeSendingService();
        var controller = CreateController(service, Guid.NewGuid());

        var result = await controller.Print(new SendDocumentDto
        {
            DocumentType = "invalid",
            Channel = "print",
            BodyHtml = "<p>Test</p>",
            RecipientIds = [Guid.NewGuid()]
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = Assert.IsType<ApiMessageResponse>(badRequest.Value);
        Assert.Equal("Type de document invalide.", payload.Message);
    }

    [Fact]
    public async Task Send_ReturnsBadRequest_WhenDocumentBodyHtmlMissingForDocumentType()
    {
        var service = new FakeSendingService();
        var controller = CreateController(service, Guid.NewGuid());

        SetJsonRequest(controller, new SendDocumentDto
        {
            DocumentType = DocumentType.TaxReceipt,
            Channel = "email",
            Subject = "Sujet",
            BodyHtml = "<p>Accompagnement</p>",
            RecipientIds = [Guid.NewGuid()]
        });

        var result = await controller.Send(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var payload = Assert.IsType<ApiMessageResponse>(badRequest.Value);
        Assert.Equal("DocumentBodyHtml is required for document types.", payload.Message);
    }

    [Fact]
    public async Task Print_ReturnsFile_WhenRequestIsValid()
    {
        var service = new FakeSendingService();
        var controller = CreateController(service, Guid.NewGuid());

        var result = await controller.Print(new SendDocumentDto
        {
            DocumentType = DocumentType.Message,
            Channel = "print",
            BodyHtml = "<p>Test</p>",
            RecipientIds = [Guid.NewGuid()]
        });

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task Print_ReturnsNotFound_WhenServiceFails()
    {
        var service = new FakeSendingService { ThrowOnPrint = true };
        var controller = CreateController(service, Guid.NewGuid());

        var result = await controller.Print(new SendDocumentDto
        {
            DocumentType = DocumentType.Message,
            Channel = "print",
            BodyHtml = "<p>Test</p>",
            RecipientIds = [Guid.NewGuid()]
        });

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var payload = Assert.IsType<ApiMessageResponse>(notFound.Value);
        Assert.Equal("Association introuvable.", payload.Message);
    }

    [Fact]
    public async Task ConfirmMailed_ReturnsNoContent_WhenRequestIsValid()
    {
        var service = new FakeSendingService();
        var controller = CreateController(service, Guid.NewGuid());
        var mailLogId = Guid.NewGuid();

        var result = await controller.ConfirmMailed(mailLogId);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(mailLogId, service.LastConfirmedMailLogId);
    }

    [Fact]
    public async Task ConfirmMailed_ReturnsNotFound_WhenServiceFails()
    {
        var service = new FakeSendingService { ThrowOnConfirm = true };
        var controller = CreateController(service, Guid.NewGuid());

        var result = await controller.ConfirmMailed(Guid.NewGuid());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var payload = Assert.IsType<ApiMessageResponse>(notFound.Value);
        Assert.Equal("Courrier introuvable.", payload.Message);
    }

    [Fact]
    public async Task Send_ReturnsBadRequest_WhenMoreThanTwoAttachments()
    {
        var service = new FakeSendingService();
        var controller = CreateController(service, Guid.NewGuid());

        var payload = JsonSerializer.Serialize(new SendDocumentDto
        {
            DocumentType = DocumentType.Message,
            Channel = "email",
            BodyHtml = "<p>Test</p>",
            RecipientIds = [Guid.NewGuid()]
        });
        SetMultipartForm(controller, payload,
        [
            CreateFormFile("a.pdf"),
            CreateFormFile("b.pdf"),
            CreateFormFile("c.pdf")
        ]);

        var result = await controller.Send(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var apiMessage = Assert.IsType<ApiMessageResponse>(badRequest.Value);
        Assert.Equal("Maximum 2 attachments allowed.", apiMessage.Message);
    }

    [Fact]
    public async Task Send_PassesUserAttachments_WhenMultipartRequestIsValid()
    {
        var service = new FakeSendingService();
        var controller = CreateController(service, Guid.NewGuid());

        var payload = JsonSerializer.Serialize(new SendDocumentDto
        {
            DocumentType = DocumentType.Message,
            Channel = "email",
            BodyHtml = "<p>Test</p>",
            RecipientIds = [Guid.NewGuid()]
        });
        SetMultipartForm(controller, payload, [CreateFormFile("brochure.pdf"), CreateFormFile("photo.jpg", "image/jpeg")]);

        var result = await controller.Send(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(service.LastUserAttachments);
        Assert.Equal(2, service.LastUserAttachments!.Count);
        Assert.Equal("brochure.pdf", service.LastUserAttachments[0].FileName);
        Assert.Equal("photo.jpg", service.LastUserAttachments[1].FileName);
    }

    private static void SetJsonRequest(SendingController controller, SendDocumentDto dto)
    {
        var json = JsonSerializer.Serialize(dto);
        controller.HttpContext.Request.ContentType = "application/json";
        controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    private static void SetMultipartForm(SendingController controller, string payload, IReadOnlyList<IFormFile> attachments)
    {
        var files = new FormFileCollection();
        foreach (var attachment in attachments)
            files.Add(attachment);

        controller.HttpContext.Request.ContentType = "multipart/form-data; boundary=test";
        controller.HttpContext.Request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                ["payload"] = payload
            },
            files);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType = "application/pdf")
    {
        var bytes = Encoding.UTF8.GetBytes("file-content");
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "attachments", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
