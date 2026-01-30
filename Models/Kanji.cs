using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

public partial class Kanji
{
    public int Id { get; set; }

    public string Literal { get; set; } = null!;

    public string UnicodeCode { get; set; } = null!;

    public short? StrokeCount { get; set; }

    public short? JlptLevel { get; set; }

    public short? GradeLevel { get; set; }

    public int? FrequencyRank { get; set; }

    public bool? IsRadical { get; set; }

    public int? WanikaniId { get; set; }
    [Column("is_active")]
    public bool? IsActive { get; set; } = true;
    public virtual ICollection<KanjiCategoryMap> KanjiCategoryMaps { get; set; } = new List<KanjiCategoryMap>();
    public virtual ICollection<KanjiAuditLog> KanjiAuditLogs { get; set; } = new List<KanjiAuditLog>();

    public virtual ICollection<KanjiDecomposition> KanjiDecompositionComponentKanjis { get; set; } = new List<KanjiDecomposition>();

    public virtual ICollection<KanjiDecomposition> KanjiDecompositionParentKanjis { get; set; } = new List<KanjiDecomposition>();

    public virtual ICollection<KanjiMeaning> KanjiMeanings { get; set; } = new List<KanjiMeaning>();

    public virtual ICollection<KanjiRadical> KanjiRadicals { get; set; } = new List<KanjiRadical>();

    public virtual ICollection<KanjiReading> KanjiReadings { get; set; } = new List<KanjiReading>();

    public virtual ICollection<KanjiSimilarity> KanjiSimilarityIdKanjiNavigations { get; set; } = new List<KanjiSimilarity>();

    public virtual ICollection<KanjiSimilarity> KanjiSimilarityIdKanjiSimilarNavigations { get; set; } = new List<KanjiSimilarity>();

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual ICollection<UserNote> UserNotes { get; set; } = new List<UserNote>();

    public virtual ICollection<UserSynonym> UserSynonyms { get; set; } = new List<UserSynonym>();

    public virtual ICollection<Radical> Radicals { get; set; } = new List<Radical>();

    public virtual ICollection<Vocabulary> Vocabularies { get; set; } = new List<Vocabulary>();
}
