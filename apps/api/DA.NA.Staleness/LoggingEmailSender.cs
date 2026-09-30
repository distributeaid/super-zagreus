using Microsoft.Extensions.Logging;

namespace DA.NA.Staleness;

/// <summary>
/// Development / prototype implementation — logs email content instead of sending.
/// Replace with SMTP or SendGrid when production email is configured.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Freshness reminder email (not sent — logging only). To={To} Subject={Subject}\n{Body}",
            message.To, message.Subject, message.Body);
        return Task.CompletedTask;
    }
}
