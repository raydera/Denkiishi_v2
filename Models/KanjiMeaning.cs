using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema; // Necessário para mapeamento se os nomes diferirem

namespace Denkiishi_v2.Models;

[Table("kanji_meaning")] // Garante que liga na tabela certa
public partial class KanjiMeaning
{
    [Column("id")]
    public int Id { get; set; }

    [Column("kanji_id")]
    public int KanjiId { get; set; }

    // O banco diz 'gloss', então aqui deve ser Gloss
    [Column("gloss")]
    public string Gloss { get; set; } = null!;

    [Column("is_principal")]
    public bool? IsPrincipal { get; set; }

    [Column("id_language")]
    public int? IdLanguage { get; set; }

    // Navegações (Relacionamentos)
    [ForeignKey("IdLanguage")]
    public virtual Language? IdLanguageNavigation { get; set; }

    [ForeignKey("KanjiId")]
    public virtual Kanji Kanji { get; set; } = null!;
}