using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class Lesson
{
    public int Id { get; set; }

    public int? IdKanji { get; set; }

    public int? IdRadical { get; set; }

    public int? VocabularyId { get; set; }

    public string? Description { get; set; }

    public string? ImagePath { get; set; }

    public virtual Kanji? IdKanjiNavigation { get; set; }

    public virtual Radical? IdRadicalNavigation { get; set; }

    public virtual Vocabulary? Vocabulary { get; set; }
}
