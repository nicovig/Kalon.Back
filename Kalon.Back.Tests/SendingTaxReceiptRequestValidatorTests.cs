using Kalon.Back.Dtos;
using Kalon.Back.Models;
using Kalon.Back.Services;

namespace Kalon.Back.Tests;

public class SendingTaxReceiptRequestValidatorTests
{
    [Fact]
    public void ValidateTaxReceiptPeriod_ThrowsWhenPeriodMissing()
    {
        var dto = new SendDocumentDto
        {
            DocumentType = DocumentType.TaxReceipt,
            Channel = "print",
            BodyHtml = "<p></p>",
            DocumentBodyHtml = "<p></p>",
            RecipientIds = [Guid.NewGuid()]
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            SendingTaxReceiptRequestValidator.ValidateTaxReceiptPeriod(dto));
        Assert.Contains("TaxReceiptPeriodFrom", ex.Message);
    }

    [Fact]
    public void ValidateTaxReceiptPeriod_DoesNotThrowForNonTaxReceipt()
    {
        var dto = new SendDocumentDto
        {
            DocumentType = DocumentType.PaymentAttestation,
            Channel = "print",
            BodyHtml = "<p></p>",
            DocumentBodyHtml = "<p></p>",
            RecipientIds = [Guid.NewGuid()]
        };

        SendingTaxReceiptRequestValidator.ValidateTaxReceiptPeriod(dto);
    }
}
