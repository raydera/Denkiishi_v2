using System;
using System.Collections.Generic;

namespace Denkiishi_v2.Models;

public partial class Radical
{
    public int Id { get; set; }

    public string Literal { get; set; } = null!;

    public string? UnicodeCode { get; set; }

    public short? KangxiNumber { get; set; }

    public short? StrokeCount { get; set; }

    public int? WanikaniId { get; set; }

    public char? PathImg { get; set; }

    public virtual ICollection<KanjiDecomposition> KanjiDecompositions { get; set; } = new List<KanjiDecomposition>();

    public virtual ICollection<KanjiRadical> KanjiRadicals { get; set; } = new List<KanjiRadical>();

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual ICollection<RadicalMeaning> RadicalMeanings { get; set; } = new List<RadicalMeaning>();

    public virtual ICollection<UserNote> UserNotes { get; set; } = new List<UserNote>();

    public virtual ICollection<UserSynonym> UserSynonyms { get; set; } = new List<UserSynonym>();

    public virtual ICollection<Kanji> Kanjis { get; set; } = new List<Kanji>();
}
