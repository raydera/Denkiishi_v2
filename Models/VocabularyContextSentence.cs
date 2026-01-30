using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class VocabularyContextSentence
{
    public int Id { get; set; }

    public int VocabularyId { get; set; }

    public string? En { get; set; }

    public string? Ja { get; set; }

    public virtual Vocabulary Vocabulary { get; set; } = null!;
}
