using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class Deck
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Card> Cards { get; set; } = new List<Card>();

    public virtual User User { get; set; } = null!;
}
