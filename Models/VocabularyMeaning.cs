using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class VocabularyMeaning
{
    public int Id { get; set; }

    public int VocabularyId { get; set; }

    public string Meaning { get; set; } = null!;

    public bool? IsPrimary { get; set; }

    public virtual Vocabulary Vocabulary { get; set; } = null!;
}
