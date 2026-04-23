using Kalon.Back.Models;

namespace Kalon.Back.Tests;

public class GeneratedDocumentAndMailLogRulesTests
{
    [Theory]
    [InlineData(true, "sent", true)]
    [InlineData(true, "error", true)]
    [InlineData(true, "printed", false)]
    [InlineData(false, "printed", true)]
    [InlineData(false, "mailed", true)]
    [InlineData(false, "sent", false)]
    public void MailLogStatuses_IsValidForChannel_MatchesChannel(bool isEmail, string status, bool expected)
    {
        Assert.Equal(expected, MailLogStatuses.IsValidForChannel(status, isEmail));
    }

    [Fact]
    public void DocumentType_RequestContract_IncludesTaxReceiptAndPaymentAttestation()
    {
        Assert.Contains(DocumentType.TaxReceipt, DocumentType.All);
        Assert.Contains(DocumentType.PaymentAttestation, DocumentType.All);
        Assert.True(DocumentType.IsValid(DocumentType.TaxReceipt));
        Assert.True(DocumentType.IsValid(DocumentType.PaymentAttestation));
        Assert.False(DocumentType.IsTaxDeductible(DocumentType.TaxReceipt));
        Assert.False(DocumentType.IsTaxDeductible(DocumentType.PaymentAttestation));
        Assert.Contains(DocumentType.Cerfa11580, DocumentType.GeneratedAll);
        Assert.Contains(DocumentType.Cerfa16216, DocumentType.GeneratedAll);
        Assert.False(DocumentType.RequiresOrderNumber(DocumentType.PaymentAttestation));
    }

    [Fact]
    public void GeneratedDocumentStatuses_AcceptsKnownValues()
    {
        Assert.True(GeneratedDocumentStatuses.IsValid(GeneratedDocumentStatuses.Pending));
        Assert.True(GeneratedDocumentStatuses.IsValid(GeneratedDocumentStatuses.Sent));
        Assert.False(GeneratedDocumentStatuses.IsValid("unknown"));
    }
}
