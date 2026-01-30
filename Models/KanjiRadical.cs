using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class KanjiRadical
{
    public int KanjiId { get; set; }

    public int RadicalId { get; set; }

    public string Role { get; set; } = null!;

    public string? Position { get; set; }

    public short? ImportanceOrd { get; set; }

    public virtual Kanji Kanji { get; set; } = null!;

    public virtual Radical Radical { get; set; } = null!;
}
