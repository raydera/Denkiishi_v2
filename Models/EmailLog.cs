using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

[Table("email_logs")]
public class EmailLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>Identity user id; vazio quando envio sem usuário (coluna user_id é NOT NULL no BD).</summary>
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Ambiente ASP.NET (Development, Production, …) — coluna environment NOT NULL no BD.</summary>
    [Required]
    [Column("environment")]
    [MaxLength(64)]
    public string AppEnvironment { get; set; } = string.Empty;

    [Required]
    [Column("email_recipient")]
    public string ToEmail { get; set; } = string.Empty;

    [Required]
    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [Column("body")]
    public string Body { get; set; } = string.Empty;

    // success | error
    [Required]
    [Column("status")]
    public string Status { get; set; } = "success";

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

