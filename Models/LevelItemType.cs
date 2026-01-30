using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class LevelItemType
{
    public int Id { get; set; }

    public string Descricao { get; set; } = null!;

    public virtual ICollection<LevelItem> LevelItems { get; set; } = new List<LevelItem>();
}
