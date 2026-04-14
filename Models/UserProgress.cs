using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

[Table("user_progress")]
public partial class UserProgress
{
    [Key]
    public int Id { get; set; }
    public string UserId { get; set; }
    public string ItemType { get; set; }
    public int ItemId { get; set; }
    public int SrsStage { get; set; }
    public DateTime UnlockedAt { get; set; }
    public DateTime NextReviewAt { get; set; }
    public DateTime PassedAt { get; set; }
    public DateTime BurnedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal EaseFactor { get; set; }
    //public int Interval { get; set; }
    //public int ReviewCount { get; set; }
    /// <summary>
    /// public int ConsecutiveCorrectCount { get; set; }
    /// </summary>
    
    
    
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("UserId")]
    public virtual ApplicationUser User { get; set; } = null!;
}