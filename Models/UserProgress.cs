namespace Denkiishi_v2.Models;

public partial class UserProgress
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public string ItemType { get; set; }
    public int ItemId { get; set; }
    public int SrsStage { get; set; }
    public decimal EaseFactor { get; set; }
    public int Interval { get; set; }
    public int ReviewCount { get; set; }
    public int ConsecutiveCorrectCount { get; set; }
    public DateTime UnlockedAt { get; set; }
    public DateTime NextReviewAt { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public DateTime? PassedAt { get; set; }
    public DateTime? BurnedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;
}