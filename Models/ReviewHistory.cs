using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

[Table("review_history")]
public partial class ReviewHistory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    public string UserId { get; set; } // String para o Identity

    [Required]
    [Column("item_type")]
    public string ItemType { get; set; } // "kanji", "radical", "vocabulary"

    [Column("item_id")]
    public int ItemId { get; set; }

    [Column("meaning_incorrect_count")]
    public int MeaningIncorrectCount { get; set; } = 0;

    [Column("reading_incorrect_count")]
    public int ReadingIncorrectCount { get; set; } = 0;

    [Column("starting_srs_stage")]
    public int StartingSrsStage { get; set; }

    [Column("ending_srs_stage")]
    public int EndingSrsStage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public virtual ApplicationUser User { get; set; } = null!;
}