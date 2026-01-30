using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class UserProgress
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int CardId { get; set; }

    public DateTime NextReviewAt { get; set; }

    public int Interval { get; set; }

    public decimal EaseFactor { get; set; }

    public int ReviewCount { get; set; }

    public int ConsecutiveCorrectCount { get; set; }

    public DateTime? LastReviewedAt { get; set; }

    public virtual Card Card { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
