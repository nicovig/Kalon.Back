using System.Security.Claims;
using Kalon.Back.Controllers;
using Kalon.Back.Dtos;
using Kalon.Back.DTOs;
using Kalon.Back.Models;
using Kalon.Back.Services.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kalon.Back.Tests;

public class SendingControllerTests
{
    private sealed class FakeSendingService : ISendingService
    {
        public bool ThrowOnPrint { get; set; }
        public bool ThrowOnConfirm { get; set; }
        public bool ThrowOnSend { get; set; }
        public Guid? LastConfirmedMailLogId { get; private set; }

        public Task<SendDocumentResultDto> SendByEmailAsync(SendDocumentDto dto, Guid organizationId)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("Association introuvable.");

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
        var controller = new SendingController(service, new FakeVariableResolverService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("organization_id", organizationId.ToString())
                ], "TestAuth"))
            }
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

        var result = await controller.Send(new SendDocumentDto
        {
            DocumentType = DocumentType.Cerfa11580,
            Channel = "email",
            Subject = "Sujet",
            BodyHtml = "<p>Accompagnement</p>",
            RecipientIds = [Guid.NewGuid()]
        });

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
}
