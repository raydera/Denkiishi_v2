using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

[Table("radical_meaning")]
public partial class RadicalMeaning
{
    [Column("id")]
    public int Id { get; set; }

    [Column("id_radical")]
    public int? IdRadical { get; set; }

    [Column("id_language")]
    public int? IdLanguage { get; set; }

    // Mantemos Description (com P) no código, mapeado para 'descrition' no banco
    [Column("descrition")]
    public string Description { get; set; }

    // Navegação para Language
    [ForeignKey("IdLanguage")]
    public virtual Language? IdLanguageNavigation { get; set; }

    // CORREÇÃO AQUI: Mudamos de 'Radical' para 'IdRadicalNavigation'
    // Isso vai satisfazer o erro no InasDbContext
    [ForeignKey("IdRadical")]
    public virtual Radical? IdRadicalNavigation { get; set; }
}