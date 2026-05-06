namespace Denkiishi_v2.Enums
{
    /// <summary>
    /// Frequência de lembretes por e-mail para revisões SRS, espelhando os registros da tabela <c>notification_frequency</c> no PostgreSQL.
    /// Cada valor inteiro é estável no banco; alterações exigem script SQL e alinhamento deste enum (abordagem Database-First).
    /// </summary>
    public enum NotificationFrequency
    {
        /// <summary>
        /// ID 0: o aluno não deseja receber lembretes; o job Hangfire ignora o usuário mesmo com itens vencidos.
        /// </summary>
        Never = 0,

        /// <summary>
        /// ID 1: no máximo um lembrete a cada ~24 horas (regra implementada com Janela de 23 horas UTC).
        /// </summary>
        Daily = 1,

        /// <summary>
        /// ID 2: cadência aproximada de duas vezes por semana (intervalo mínimo de 3 dias entre envios).
        /// </summary>
        TwiceAWeek = 2,

        /// <summary>
        /// ID 3: lembrete semanal (intervalo mínimo de 6 dias entre envios).
        /// </summary>
        Weekly = 3
    }
}
