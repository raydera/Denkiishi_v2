using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class KanjiDecomposition
{
    public int Id { get; set; }

    public int ParentKanjiId { get; set; }

    public string ComponentType { get; set; } = null!;

    public int? ComponentRadicalId { get; set; }

    public int? ComponentKanjiId { get; set; }

    public string? RelationType { get; set; }

    public short? DepthLevel { get; set; }

    public short? OrderIndex { get; set; }

    public virtual Kanji? ComponentKanji { get; set; }

    public virtual Radical? ComponentRadical { get; set; }

    public virtual Kanji ParentKanji { get; set; } = null!;
}
