using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

[Table("quiz_sessions")] // Força o mapeamento para o nome exato da tabela no Postgres
public class QuizSession
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("user_id")]
    public string UserId { get; set; } // String para bater com Identity (AspNetUsers)

    [Required]
    [Column("current_state", TypeName = "jsonb")] // Essencial para o Npgsql entender o formato
    public string CurrentState { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}