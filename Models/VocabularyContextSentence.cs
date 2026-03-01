using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

public partial class VocabularyContextSentence
{
    public int Id { get; set; }
    public int VocabularyId { get; set; }

    // Texto em Japonês (Ajustado para bater com o Controller)
    [Column("ja")]
    public string? Jp { get; set; }

    // Texto traduzido (Ajustado para bater com o Controller)
    public string? En { get; set; }

    public int? LanguageId { get; set; }

    public virtual Vocabulary Vocabulary { get; set; } = null!;
    public virtual Language? Language { get; set; }
}