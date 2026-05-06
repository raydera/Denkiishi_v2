using Denkiishi_v2.Enums;
using Denkiishi_v2.Models;
using Denkiishi_v2.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TimeZoneConverter;

namespace Denkiishi_v2.Jobs;

/// <summary>
/// Job Hangfire responsável por notificar alunos com itens SRS vencidos (<see cref="UserProgress.NextReviewAt"/>).
/// </summary>
/// <remarks>
/// <para><b>E-mail dinâmico (banco de dados)</b></para>
/// <para>
/// O assunto e o corpo HTML das notificações são obtidos da tabela <c>public.email_templates</c>,
/// chave <see cref="SrsReviewReminderTemplateCode"/>, permitindo ajustar textos sem redeploy.
/// Placeholders no HTML são substituídos em tempo de execução; se o template não existir, estiver incompleto
/// ou a leitura falhar, utiliza-se um conjunto fixo de fallback seguro e o evento é registrado no log.
/// </para>
/// <para><b>Filtro temporal (por que existem estes intervalos?)</b></para>
/// <para>
/// O aluno escolhe uma <see cref="NotificationFrequency"/>; o job não deve bombardear a caixa de entrada.
/// Para cada frequência comparamos <see cref="ApplicationUser.LastNotificationSentAt"/> com o relógio UTC:
/// </para>
/// <list type="bullet">
/// <item><b>Diário:</b> permite novo envio se o último foi há mais de 23 horas (margem para variação do horário do job e fuso do servidor).</item>
/// <item><b>Duas vezes por semana:</b> mínimo de 3 dias corridos entre envios — aproxima duas notificações por semana sem exigir calendário civil.</item>
/// <item><b>Semanal:</b> mínimo de 6 dias corridos entre envios.</item>
/// </list>
/// <para>
/// Valores desconhecidos ou fora do intervalo esperado são tratados de forma defensiva (não envia) para evitar spam acidental após mudanças no banco.
/// </para>
/// </remarks>
public class SrsNotificationJob
{
    /// <summary>
    /// Código lógico do template de lembrete SRS em <c>email_templates.code</c>.
    /// </summary>
    public const string SrsReviewReminderTemplateCode = "SRS_REVIEW_REMINDER";

    private const string FallbackSubject = "Lembrete de Revisão - Denkiishi";

    private const string FallbackBodyHtml =
        "Olá, {Nickname}!<br/><br/>" +
        "Você tem <strong>{ItemCount}</strong> item(ns) pendente(s) para revisão.<br/><br/>" +
        "A sua revisão vence às <strong>{DueDate}</strong> (Horário de {TimeZone}).<br/><br/>" +
        "Acesse o Denkiishi e faça suas revisões para manter o progresso!<br/><br/>" +
        "Atenciosamente,<br/>Equipe Denkiishi";

    private readonly InasDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<SrsNotificationJob> _logger;

    /// <summary>
    /// Inicializa o job com o contexto do banco (incluindo <c>email_templates</c>), o serviço de e-mail e o logger de aplicação.
    /// </summary>
    /// <param name="context">Contexto EF para consultar progresso SRS, templates de e-mail e atualizar <see cref="ApplicationUser.LastNotificationSentAt"/>.</param>
    /// <param name="emailService">Serviço que envia o e-mail (HTML quando aplicável) e registra em <c>email_logs</c>.</param>
    /// <param name="logger">Logger estruturado para diagnóstico do processamento em lote.</param>
    public SrsNotificationJob(InasDbContext context, IEmailService emailService, ILogger<SrsNotificationJob> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Executa um ciclo de notificação: localiza usuários com revisões atrasadas, aplica a política de frequência por aluno,
    /// carrega o template de e-mail para SRS (ou fallback), envia quando permitido e persiste <see cref="ApplicationUser.LastNotificationSentAt"/> em UTC após envio bem-sucedido.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento cooperativo.</param>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var template = await TryLoadSrsReminderTemplateAsync(cancellationToken);
        var subjectTemplate = template?.Subject ?? FallbackSubject;
        var bodyTemplate = template?.BodyHtml ?? FallbackBodyHtml;

        var now = DateTime.UtcNow;

        var dueUserIds = await _context.UserProgresses
            .AsNoTracking()
            .Where(p => p.NextReviewAt <= now)
            .Select(p => p.UserId)
            .Distinct()
            .OrderBy(id => id)
            .Take(1000)
            .ToListAsync(cancellationToken);

        if (dueUserIds.Count == 0)
        {
            _logger.LogInformation("SrsNotificationJob: sem usuários com revisões pendentes.");
            return;
        }

        const int batchSize = 50;
        for (int i = 0; i < dueUserIds.Count; i += batchSize)
        {
            var batch = dueUserIds.Skip(i).Take(batchSize).ToList();

            var counts = await _context.UserProgresses
                .AsNoTracking()
                .Where(p => batch.Contains(p.UserId) && p.NextReviewAt <= now)
                .GroupBy(p => p.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), NextReviewAtUtc = g.Min(x => x.NextReviewAt) })
                .ToListAsync(cancellationToken);

            var countsByUser = counts.ToDictionary(x => x.UserId, x => (x.Count, x.NextReviewAtUtc));

            var users = await _context.Set<ApplicationUser>()
                .AsNoTracking()
                .Where(u => batch.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Nickname,
                    u.UserName,
                    u.TimeZone,
                    u.NotificationFrequencyId,
                    u.LastNotificationSentAt
                })
                .ToListAsync(cancellationToken);

            foreach (var u in users)
            {
                if (string.IsNullOrWhiteSpace(u.Email))
                    continue;

                if (!countsByUser.TryGetValue(u.Id, out var data) || data.Count <= 0)
                    continue;

                if (!ShouldSendNotification(u.NotificationFrequencyId, u.LastNotificationSentAt, now))
                    continue;

                var displayName = string.IsNullOrWhiteSpace(u.Nickname) ? (u.UserName ?? "aluno") : u.Nickname;

                var tzId = string.IsNullOrWhiteSpace(u.TimeZone) ? "UTC" : u.TimeZone;
                TimeZoneInfo tzInfo;
                try
                {
                    tzInfo = TZConvert.GetTimeZoneInfo(tzId);
                }
                catch
                {
                    tzId = "UTC";
                    tzInfo = TimeZoneInfo.Utc;
                }

                var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(data.NextReviewAtUtc, DateTimeKind.Utc), tzInfo);
                var dueDateFormatted = localTime.ToString("dd/MM/yyyy HH:mm");

                var subject = ApplySrsPlaceholders(subjectTemplate, displayName, data.Count, dueDateFormatted, tzId);
                var bodyHtml = ApplySrsPlaceholders(bodyTemplate, displayName, data.Count, dueDateFormatted, tzId);

                try
                {
                    await _emailService.SendAsync(u.Email, subject, bodyHtml, userId: u.Id, isHtml: true, cancellationToken);
                    await MarkNotificationSentAsync(u.Id, now, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao enviar notificação SRS para UserId={UserId}", u.Id);
                }
            }
        }
    }

    /// <summary>
    /// Carrega assunto e corpo do template SRS em <c>email_templates</c>. Falhas de banco ou linha ausente retornam <see langword="null"/> sem propagar exceção.
    /// </summary>
    private async Task<SrsReminderTemplateSnapshot?> TryLoadSrsReminderTemplateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var row = await _context.EmailTemplates
                .AsNoTracking()
                .Where(t => t.Code == SrsReviewReminderTemplateCode)
                .Select(t => new { t.Subject, t.BodyHtml })
                .FirstOrDefaultAsync(cancellationToken);

            if (row == null)
            {
                _logger.LogError(
                    "Template de e-mail não encontrado no banco (Code={Code}). Usando assunto e HTML de fallback.",
                    SrsReviewReminderTemplateCode);
                return null;
            }

            if (string.IsNullOrWhiteSpace(row.Subject) || string.IsNullOrWhiteSpace(row.BodyHtml))
            {
                _logger.LogError(
                    "Template de e-mail inválido: subject ou body_html vazio (Code={Code}). Usando fallback.",
                    SrsReviewReminderTemplateCode);
                return null;
            }

            return new SrsReminderTemplateSnapshot(row.Subject, row.BodyHtml);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao carregar template de e-mail do banco (Code={Code}). Usando assunto e HTML de fallback.",
                SrsReviewReminderTemplateCode);
            return null;
        }
    }

    /// <summary>
    /// Substitui os placeholders documentados no HTML ou no assunto: <c>{Nickname}</c>, <c>{ItemCount}</c>, <c>{DueDate}</c>, <c>{TimeZone}</c>.
    /// </summary>
    private static string ApplySrsPlaceholders(
        string template,
        string nickname,
        int itemCount,
        string dueDate,
        string timeZoneId)
    {
        return template
            .Replace("{Nickname}", nickname, StringComparison.Ordinal)
            .Replace("{ItemCount}", itemCount.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{DueDate}", dueDate, StringComparison.Ordinal)
            .Replace("{TimeZone}", timeZoneId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Atualiza apenas <see cref="ApplicationUser.LastNotificationSentAt"/> para o instante do envio bem-sucedido (UTC),
    /// sem carregar a entidade completa em memória.
    /// </summary>
    /// <param name="userId">Identificador do usuário Identity.</param>
    /// <param name="sentAtUtc">Instante a gravar (normalmente <see cref="DateTime.UtcNow"/> do ciclo do job).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    private async Task MarkNotificationSentAsync(string userId, DateTime sentAtUtc, CancellationToken cancellationToken)
    {
        await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(u => u.LastNotificationSentAt, sentAtUtc),
                cancellationToken);
    }

    /// <summary>
    /// Indica se o envio do lembrete está permitido para o par frequência / último envio,
    /// conforme regras de negócio do SRS e da tabela de frequências no banco.
    /// </summary>
    /// <param name="notificationFrequencyId">Valor inteiro persistido em <c>AspNetUsers.notification_frequency_id</c>.</param>
    /// <param name="lastNotificationSentAt">Último envio registrado; <see langword="null"/> autoriza o primeiro envio (exceto frequência &quot;Nunca&quot;).</param>
    /// <param name="utcNow">Referência temporal atual em UTC (mesma base usada ao gravar o próximo envio).</param>
    /// <returns><see langword="true"/> se o e-mail pode ser disparado neste ciclo; caso contrário, <see langword="false"/>.</returns>
    private static bool ShouldSendNotification(int notificationFrequencyId, DateTime? lastNotificationSentAt, DateTime utcNow)
    {
        if (notificationFrequencyId == (int)NotificationFrequency.Never)
            return false;

        switch (notificationFrequencyId)
        {
            case (int)NotificationFrequency.Daily:
                return lastNotificationSentAt == null || lastNotificationSentAt < utcNow.AddHours(-23);

            case (int)NotificationFrequency.TwiceAWeek:
                return lastNotificationSentAt == null || lastNotificationSentAt < utcNow.AddDays(-3);

            case (int)NotificationFrequency.Weekly:
                return lastNotificationSentAt == null || lastNotificationSentAt < utcNow.AddDays(-6);

            default:
                return false;
        }
    }

    /// <summary>
    /// Snapshot imutável do template SRS carregado do banco (apenas campos usados no envio).
    /// </summary>
    private sealed record SrsReminderTemplateSnapshot(string Subject, string BodyHtml);
}
