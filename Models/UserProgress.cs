using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

[Table("user_progress")]
public partial class UserProgress
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    public string UserId { get; set; } // Alterado para string para alinhar com AspNetUsers (Identity)

    [Column("item_type")]
    [StringLength(20)]
    public string ItemType { get; set; } // "kanji", "radical" ou "vocabulary"

    [Column("item_id")]
    public int ItemId { get; set; }

    [Column("srs_stage")]
    public int SrsStage { get; set; } = 0;

    [Column("ease_factor")]
    public decimal EaseFactor { get; set; } = 2.50m;

    [Column("interval")]
    public int Interval { get; set; } = 0;

    [Column("review_count")]
    public int ReviewCount { get; set; } = 0;

    [Column("consecutive_correct_count")]
    public int ConsecutiveCorrectCount { get; set; } = 0;

    [Column("unlocked_at")]
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;

    [Column("next_review_at")]
    public DateTime NextReviewAt { get; set; }

    [Column("last_reviewed_at")]
    public DateTime? LastReviewedAt { get; set; }

    [Column("passed_at")]
    public DateTime? PassedAt { get; set; }

    [Column("burned_at")]
    public DateTime? BurnedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Propriedades de Navegação
    [ForeignKey("UserId")]
    public virtual ApplicationUser User { get; set; } = null!;

    // CardId mantido apenas para compatibilidade de esquema se necessário, 
    // mas não será usado pela nova lógica do Quiz.
    [Column("card_id")]
    public int? CardId { get; set; }
}