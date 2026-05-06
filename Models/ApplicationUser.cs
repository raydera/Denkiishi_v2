using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    // Herdamos de IdentityUser para ganhar nome, email, senha hash, etc.
    public class ApplicationUser : IdentityUser
    {
        // Aqui podemos adicionar campos extras no futuro.
        // Por exemplo: public string NivelJapones { get; set; }
        [StringLength(50)]
        public string Nickname { get; set; } // Nova propriedade
                                             // Em Models/ApplicationUser.cs

        public string TimeZone { get; set; } = "UTC";

        public string? Country { get; set; }

        /// <summary>
        /// Identificador da preferência de frequência de lembretes por e-mail do SRS (FK lógica para <c>notification_frequency</c>).
        /// Valores alinhados a <see cref="Denkiishi_v2.Enums.NotificationFrequency"/> (0 = nunca, 1 = diário, etc.).
        /// O job em Hangfire usa este campo para decidir se deve enviar nova notificação sem violar o intervalo escolhido pelo aluno.
        /// </summary>
        [Column("notification_frequency_id")]
        public int NotificationFrequencyId { get; set; } = 1;

        /// <summary>
        /// Momento UTC do último e-mail de lembrete SRS enviado (ou tentado com sucesso pelo serviço de e-mail).
        /// Usado pelo job agendado para aplicar a cadência mínima entre envios conforme <see cref="NotificationFrequencyId"/>.
        /// <see langword="null"/> indica que nunca houve envio registrado; nesse caso a primeira notificação pode ser disparada
        /// assim que houver itens vencidos e a frequência não for &quot;Nunca&quot;.
        /// </summary>
        [Column("last_notification_sent_at")]
        public DateTime? LastNotificationSentAt { get; set; }
        public virtual ICollection<UserProgress> UserProgresses { get; set; } = new List<UserProgress>();
        public virtual ICollection<ReviewHistory> ReviewHistories { get; set; } = new List<ReviewHistory>();
    }
}
