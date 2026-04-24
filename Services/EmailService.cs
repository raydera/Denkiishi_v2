using Denkiishi_v2.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Denkiishi_v2.Services;

public class EmailService : IEmailService
{
    private readonly InasDbContext _context;
    private readonly EmailSettings _settings;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        InasDbContext context,
        IOptions<EmailSettings> settings,
        IHostEnvironment hostEnvironment,
        ILogger<EmailService> logger)
    {
        _context = context;
        _settings = settings.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body, string? userId = null, CancellationToken cancellationToken = default)
    {
        var log = new EmailLog
        {
            UserId = userId ?? string.Empty,
            AppEnvironment = _hostEnvironment.EnvironmentName,
            ToEmail = toEmail,
            Subject = subject,
            Body = body,
            Status = "success",
            CreatedAt = DateTime.UtcNow
        };

        Exception? smtpFailure = null;
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.SenderEmail))
                throw new InvalidOperationException("EmailSettings: SenderEmail não configurado.");

            if (string.IsNullOrWhiteSpace(_settings.AppPassword))
                throw new InvalidOperationException("EmailSettings: AppPassword não configurado (use variável de ambiente ou User Secrets).");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.Server, _settings.Port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.AppPassword, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            log.Status = "error";
            log.ErrorMessage = ex.Message;
            smtpFailure = ex;
        }

        try
        {
            _context.EmailLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha ao gravar em email_logs após tentativa de envio (recipient={Recipient}, status={Status}). " +
                "O e-mail pode ter sido enviado mesmo assim. Confira NOT NULL sem default (ex.: environment, user_id) e demais colunas do modelo.",
                log.ToEmail,
                log.Status);
            throw;
        }

        if (smtpFailure != null)
            throw smtpFailure;
    }
}
