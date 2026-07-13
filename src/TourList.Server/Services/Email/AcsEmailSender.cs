using Azure;
using Azure.Communication.Email;
using Azure.Core;
using Microsoft.Extensions.Options;
using TourList.Server.Options;

namespace TourList.Server.Services.Email;

/// <summary>
/// Sends transactional email via Azure Communication Services. Prefers managed identity
/// (<c>Endpoint</c> + <see cref="TokenCredential"/>); falls back to a connection string if provided.
/// When ACS isn't configured, it logs the link and returns — password recovery still works in dev.
/// </summary>
public sealed class AcsEmailSender : IEmailDispatcher
{
    private readonly AcsEmailOptions _options;
    private readonly ILogger<AcsEmailSender> _logger;
    private readonly EmailClient? _client;

    public AcsEmailSender(IOptions<AcsEmailOptions> options, TokenCredential credential, ILogger<AcsEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            _client = new EmailClient(_options.ConnectionString);
        }
        else if (!string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            _client = new EmailClient(new Uri(_options.Endpoint), credential);
        }
    }

    public async Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default)
    {
        if (_client is null || string.IsNullOrWhiteSpace(_options.SenderAddress))
        {
            _logger.LogInformation("ACS Email not configured; password reset link for {Email}: {Link}", toEmail, resetLink);
            return;
        }

        var content = new EmailContent("Reset your TourList password")
        {
            PlainText = $"Reset your TourList password using this link:\n{resetLink}\n\nIf you didn't request this, you can ignore this email.",
            Html = $"<p>Reset your TourList password:</p><p><a href=\"{resetLink}\">Reset password</a></p><p>If you didn't request this, you can ignore this email.</p>",
        };

        try
        {
            var message = new EmailMessage(_options.SenderAddress, toEmail, content);
            await _client.SendAsync(WaitUntil.Started, message, ct);
            _logger.LogInformation("Queued password reset email to {Email}.", toEmail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never fail the request (and never reveal existence) — log for diagnosis.
            _logger.LogError(ex, "Failed to send password reset email to {Email}.", toEmail);
        }
    }
}
