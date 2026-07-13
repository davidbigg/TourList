namespace TourList.Server.Services.Email;

/// <summary>Sends TourList transactional email (named to avoid clashing with Identity's IEmailSender).</summary>
public interface IEmailDispatcher
{
    Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default);
}
