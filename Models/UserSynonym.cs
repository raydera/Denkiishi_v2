using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class UserSynonym
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int KanjiId { get; set; }

    public int RadicalId { get; set; }

    public string? SynonymText { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Kanji Kanji { get; set; } = null!;

    public virtual Radical Radical { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
