using Kalon.Back.Dtos;
using Kalon.Back.Services.Mail;

namespace Kalon.Back.Tests;

public class OnboardingMailServiceTests
{
    private sealed class CapturingMailService : IMailService
    {
        public MailMessageDto? LastMessage { get; private set; }

        public Task SendAsync(MailMessageDto message)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SendWelcomeAsync_SendsMailWithCredentials()
    {
        var mail = new CapturingMailService();
        var service = new OnboardingMailService(mail);

        await service.SendWelcomeAsync(new WelcomeMailData
        {
            Firstname = "Marie",
            Email = "marie@asso.fr",
            AssociationName = "Asso Test",
            TemporaryPassword = "TempPass1!"
        }, CancellationToken.None);

        Assert.NotNull(mail.LastMessage);
        Assert.Equal("marie@asso.fr", mail.LastMessage!.ToEmail);
        Assert.Equal("Marie", mail.LastMessage.ToName);
        Assert.Contains("Asso Test", mail.LastMessage.Subject);
        Assert.Contains("marie@asso.fr", mail.LastMessage.BodyHtml);
        Assert.Contains("TempPass1!", mail.LastMessage.BodyHtml);
        Assert.Contains("https://app.kalon-app.fr", mail.LastMessage.BodyHtml);
    }
}
