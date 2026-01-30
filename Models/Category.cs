using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models;

[Table("category")]
public class Category
{
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!; // Ex: "JLPT", "Jouyou"

    [Column("description")]
    public string? Description { get; set; }

    // Relação via tabela intermediária explicita
    public virtual ICollection<KanjiCategoryMap> KanjiCategoryMaps { get; set; } = new List<KanjiCategoryMap>();
}