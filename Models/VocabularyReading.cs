using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class VocabularyReading
{
    public int Id { get; set; }

    public int VocabularyId { get; set; }

    public string Reading { get; set; } = null!;

    public bool? IsPrimary { get; set; }

    public virtual Vocabulary Vocabulary { get; set; } = null!;
}
