using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class KanjiSimilarity
{
    public int Id { get; set; }

    public int? IdKanji { get; set; }

    public int? IdKanjiSimilar { get; set; }

    public virtual Kanji? IdKanjiNavigation { get; set; }

    public virtual Kanji? IdKanjiSimilarNavigation { get; set; }
}
