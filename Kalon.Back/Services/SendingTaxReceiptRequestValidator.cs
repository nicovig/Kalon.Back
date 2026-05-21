using Kalon.Back.Dtos;
using Kalon.Back.Models;

namespace Kalon.Back.Services;

public static class SendingTaxReceiptRequestValidator
{
    public static void ValidateTaxReceiptPeriod(SendDocumentDto dto)
    {
        if (dto.DocumentType != DocumentType.TaxReceipt)
            return;

        if (!dto.TaxReceiptPeriodFrom.HasValue || !dto.TaxReceiptPeriodTo.HasValue)
            throw new ArgumentException(
                "TaxReceiptPeriodFrom et TaxReceiptPeriodTo sont requis pour un reçu fiscal.");

        TaxReceiptPeriodHelper.Create(dto.TaxReceiptPeriodFrom.Value, dto.TaxReceiptPeriodTo.Value);
    }
}
