namespace Kalon.Back.Models;

public class MailLog
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; }

    public Guid ContactId { get; set; }
    public Contact Contact { get; set; }

    public Guid? GeneratedDocumentId { get; set; }
    public GeneratedDocument? GeneratedDocument { get; set; }

    public bool IsEmail { get; set; } = true;

    // si IsEmail == true  : adresse email du destinataire au moment de l'envoi
    // si IsEmail == false : null
    public string? SentToEmail { get; set; }

    public string Subject { get; set; }
    public string Body { get; set; }

    // si IsEmail == true  : "sent" | "error"
    // si IsEmail == false : "printed" | "mailed"
    public string Status { get; set; }

    public string? ErrorMessage { get; set; }   // si Status == "error"

    public DateTime? PrintedAt { get; set; }    // si IsEmail == false — PDF généré
    public DateTime? MailedAt { get; set; }     // si IsEmail == false — confirmé envoyé
    public string? MailedBy { get; set; }       // prénom de qui a confirmé physiquement

    public DateTime CreatedAt { get; set; }
}

public static class MailLogStatuses
{
    // email
    public const string Sent = "sent";
    public const string Error = "error";

    // courrier papier
    public const string Printed = "printed";   // PDF généré, pas encore posté
    public const string Mailed = "mailed";    // confirmé envoyé physiquement

    public static readonly IReadOnlyList<string> All = new[]
    { Sent, Error, Printed, Mailed };

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value);

    // validation croisée avec IsEmail
    public static bool IsValidForChannel(string? value, bool isEmail) =>
        isEmail
            ? value is Sent or Error
            : value is Printed or Mailed;
}