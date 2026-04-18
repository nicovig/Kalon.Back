using Kalon.Back.Models;

namespace Kalon.Back.Services.Mail;

public class PrintPageData
{
    public Contact Contact { get; set; }
    public Organization Organization { get; set; }
    public string ResolvedHtml { get; set; }       // corps déjà résolu par VariableResolver
    public string DocumentType { get; set; }
    public ContentBlock? SignatureBlock { get; set; }
    public GeneratedDocument? GeneratedDocument { get; set; }
}