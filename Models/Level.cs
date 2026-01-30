using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class Level
{
    public int Id { get; set; }

    public string? Description { get; set; }

    public string? Comment { get; set; }

    public virtual ICollection<LevelItem> LevelItems { get; set; } = new List<LevelItem>();
}
