using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

/// <summary>
/// Representa uma linha em <c>public.email_templates</c>: layout HTML e metadados para e-mails transacionais,
/// permitindo alterar textos sem novo deploy da aplicação.
/// </summary>
[Table("email_templates")]
public class EmailTemplate
{
    /// <summary>
    /// Chave primária autoincremental (coluna <c>id</c>).
    /// </summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// Chave lógica única utilizada pelo backend para localizar o template (coluna <c>code</c>), por exemplo <c>SRS_REVIEW_REMINDER</c>.
    /// </summary>
    [Required]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Texto exibido no campo Assunto do e-mail (coluna <c>subject</c>).
    /// </summary>
    [Required]
    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Corpo do e-mail em HTML (coluna <c>body_html</c>). Aceita placeholders no formato <c>{NomeVariavel}</c> substituídos em tempo de envio.
    /// </summary>
    [Required]
    [Column("body_html")]
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>
    /// Descrição interna do propósito do template e quando é disparado (coluna <c>description</c>).
    /// </summary>
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Data de criação do registro no banco (coluna <c>created_at</c>).
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data da última alteração do template (coluna <c>updated_at</c>).
    /// </summary>
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
