using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class ReviewHistory
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int CardId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public int Rating { get; set; }

    public int IntervalUsed { get; set; }

    public virtual Card Card { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
