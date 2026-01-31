using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class VocabularyComposition
{
    public int Id { get; set; }
    public int VocabularyId { get; set; }
    public int KanjiId { get; set; }

    public virtual Kanji Kanji { get; set; } = null!;
    public virtual Vocabulary Vocabulary { get; set; } = null!;
}