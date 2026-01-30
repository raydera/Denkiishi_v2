using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class Card
{
    public int Id { get; set; }

    public int DeckId { get; set; }

    public string Front { get; set; } = null!;

    public string Back { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual Deck Deck { get; set; } = null!;

    public virtual ICollection<ReviewHistory> ReviewHistories { get; set; } = new List<ReviewHistory>();

    public virtual ICollection<UserProgress> UserProgresses { get; set; } = new List<UserProgress>();
}
