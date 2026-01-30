using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class LevelItem
{
    public int Id { get; set; }

    public int IdNivelItenObjeto { get; set; }

    public int IdNivel { get; set; }

    public int IdObjeto { get; set; }

    public virtual LevelItemType IdNivelItenObjetoNavigation { get; set; } = null!;

    public virtual Level IdNivelNavigation { get; set; } = null!;
}
