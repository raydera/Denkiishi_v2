using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    [Table("vocabulary_category_map")]
    public class VocabularyCategoryMap
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("vocabulary_id")]
        public int VocabularyId { get; set; }

        [Column("category_id")]
        public int CategoryId { get; set; }

        [Column("category_level")]
        public string CategoryLevel { get; set; }

        [Column("incl_date")]
        public DateTime InclDate { get; set; } = DateTime.UtcNow;

        // Propriedades de Navegação (Relacionamentos)
        [ForeignKey("VocabularyId")]
        public virtual Vocabulary Vocabulary { get; set; }

        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }
    }
}