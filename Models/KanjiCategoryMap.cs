using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

[Table("kanji_category_map")]
public class KanjiCategoryMap
{
    [Column("kanji_id")]
    public int KanjiId { get; set; }

    [Column("category_id")]
    public int CategoryId { get; set; }

    // Aqui estão as colunas extras que você criou!
    [Column("category_level")]
    public string? CategoryLevel { get; set; } // Ex: "N5", "1", "High School"

    [Column("incl_date")]
    public DateTime InclDate { get; set; } = DateTime.UtcNow;

    // Navegação
    [ForeignKey("KanjiId")]
    public virtual Kanji Kanji { get; set; } = null!;

    [ForeignKey("CategoryId")]
    public virtual Category Category { get; set; } = null!;
}