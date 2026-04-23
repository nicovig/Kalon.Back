namespace Kalon.Back.DTOs;

public class AiMailRequestDto
{
    // prompt libre saisi par l'utilisateur dans l'UI
    public string UserContext { get; set; } = "";

    // "reminder" | "thank_you" | "emergency" | "seasonal"
    // | "anniversary" | "renewal" | "fidelisation" | "other"
    public string EmailType { get; set; } = "";
}

public class AiMailResultDto
{
    // objet du mail — peut contenir {{prenom}}
    public string Subject { get; set; } = "";

    // corps HTML prêt à injecter dans TipTap
    // balises autorisées : <p> <strong> <em> <br>
    public string BodyHtml { get; set; } = "";
}