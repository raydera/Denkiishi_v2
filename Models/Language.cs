using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class Language
{
    public int Id { get; set; }

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public string? LanguageCode { get; set; }

    public virtual ICollection<KanjiMeaning> KanjiMeanings { get; set; } = new List<KanjiMeaning>();

    public virtual ICollection<RadicalMeaning> RadicalMeanings { get; set; } = new List<RadicalMeaning>();
}
