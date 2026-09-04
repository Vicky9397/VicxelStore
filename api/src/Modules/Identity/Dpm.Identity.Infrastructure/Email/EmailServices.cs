using System.Net.Mail;
using Dpm.Identity.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dpm.Identity.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 1025;

    public string FromAddress { get; init; } = "no-reply@vicxelstore.local";

    public string FromName { get; init; } = "VicxelStore";
}

/// <summary>Plain SMTP sender; targets MailHog in local dev (docker-compose).</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        var smtp = options.Value;
        using var client = new SmtpClient(smtp.Host, smtp.Port);
        using var message = new MailMessage
        {
            From = new MailAddress(smtp.FromAddress, smtp.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(to);
        await client.SendMailAsync(message, ct);
    }
}

/// <summary>Fallback when no SMTP host is configured: logs instead of sending.</summary>
public sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        LogEmail(logger, to, subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email suppressed (no SMTP host): to={To} subject={Subject}")]
    private static partial void LogEmail(ILogger logger, string to, string subject);
}

public sealed class AppLinkOptions
{
    public const string SectionName = "App";

    public string WebBaseUrl { get; init; } = "http://localhost:5173";
}

public sealed class VerificationEmailComposer(
    IEmailSender emailSender,
    IOptions<AppLinkOptions> appOptions)
    : IVerificationEmailComposer
{
    public Task SendVerificationAsync(string email, string displayName, string token, CancellationToken ct)
    {
        var link = $"{appOptions.Value.WebBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(token)}";
        var body =
            $"<p>Hi {System.Net.WebUtility.HtmlEncode(displayName)},</p>" +
            $"<p>Confirm your email address to activate your VicxelStore account. " +
            $"This link expires in 24 hours.</p>" +
            $"<p><a href=\"{link}\">Verify email</a></p>" +
            $"<p>If you did not create this account, ignore this message.</p>";
        return emailSender.SendAsync(email, "Verify your email", body, ct);
    }
}
