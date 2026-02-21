using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

public partial class KanjiReading
{
    public int Id { get; set; }

    public int KanjiId { get; set; }

    public string Type { get; set; } = null!;

    public string ReadingKana { get; set; } = null!;

    public string? ReadingRomaji { get; set; }

    public virtual Kanji Kanji { get; set; } = null!;
    [Column("is_principal")]
    public bool? IsPrincipal { get; set; }
}
