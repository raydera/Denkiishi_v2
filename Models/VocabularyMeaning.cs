using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class VocabularyMeaning
{
    public int Id { get; set; }
    public int VocabularyId { get; set; }
    public string Meaning { get; set; } = null!;

    // Campos necessários para a tradução por idioma
    public int LanguageId { get; set; }
    public string? Type { get; set; } // 'primary', 'alternative', etc.
    public bool IsPrimary { get; internal set; }
    public virtual Vocabulary Vocabulary { get; set; } = null!;
    public virtual Language Language { get; set; } = null!;
   
}