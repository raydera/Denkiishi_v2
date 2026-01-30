using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class Vocabulary
{
    public int Id { get; set; }

    public int? WanikaniId { get; set; }

    public string Characters { get; set; } = null!;

    public short? Level { get; set; }

    public string? MeaningMnemonic { get; set; }

    public string? ReadingMnemonic { get; set; }

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual ICollection<VocabularyContextSentence> VocabularyContextSentences { get; set; } = new List<VocabularyContextSentence>();

    public virtual ICollection<VocabularyMeaning> VocabularyMeanings { get; set; } = new List<VocabularyMeaning>();

    public virtual ICollection<VocabularyReading> VocabularyReadings { get; set; } = new List<VocabularyReading>();

    public virtual ICollection<Kanji> Kanjis { get; set; } = new List<Kanji>();
}
