using Denkiishi_v2.Models;
using Denkiishi_v2.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TimeZoneConverter;

namespace Denkiishi_v2.Jobs;

public class SrsNotificationJob
{
    private readonly InasDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<SrsNotificationJob> _logger;

    public SrsNotificationJob(InasDbContext context, IEmailService emailService, ILogger<SrsNotificationJob> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Performance: não carregar tudo; buscar IDs distintos e processar em lotes
        var dueUserIds = await _context.UserProgresses
            .AsNoTracking()
            .Where(p => p.NextReviewAt <= now)
            .Select(p => p.UserId)
            .Distinct()
            .OrderBy(id => id)
            .Take(1000) // limite de segurança (pode ajustar)
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

            // conta quantos itens vencidos cada usuário tem
            var counts = await _context.UserProgresses
                .AsNoTracking()
                .Where(p => batch.Contains(p.UserId) && p.NextReviewAt <= now)
                .GroupBy(p => p.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), NextReviewAtUtc = g.Min(x => x.NextReviewAt) })
                .ToListAsync(cancellationToken);

            var countsByUser = counts.ToDictionary(x => x.UserId, x => (x.Count, x.NextReviewAtUtc));

            // Busca emails no Identity
            var users = await _context.Set<ApplicationUser>()
                .AsNoTracking()
                .Where(u => batch.Contains(u.Id))
                .Select(u => new { u.Id, u.Email, u.Nickname, u.UserName, u.TimeZone })
                .ToListAsync(cancellationToken);

            foreach (var u in users)
            {
                if (string.IsNullOrWhiteSpace(u.Email)) continue;
                if (!countsByUser.TryGetValue(u.Id, out var data) || data.Count <= 0) continue;

                var displayName = string.IsNullOrWhiteSpace(u.Nickname) ? (u.UserName ?? "aluno") : u.Nickname;

                // Converte o horário UTC para o fuso do usuário (suporta Windows e IANA)
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
                var horaLocal = localTime.ToString("dd/MM/yyyy HH:mm");

                var subject = "Denkiishi: revisões pendentes no SRS";
                var body =
$@"Olá, {displayName}!

Você tem {data.Count} item(ns) pendente(s) para revisão.

A sua revisão vence às {horaLocal} (Horário de {tzId}).

Acesse o Denkiishi e faça suas revisões para manter o progresso!

Atenciosamente,
Equipe Denkiishi";

                try
                {
                    await _emailService.SendAsync(u.Email, subject, body, userId: u.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    // EmailService já registra em email_logs com status=error; aqui apenas logamos no app
                    _logger.LogError(ex, "Erro ao enviar notificação SRS para UserId={UserId}", u.Id);
                }
            }
        }
    }
}

