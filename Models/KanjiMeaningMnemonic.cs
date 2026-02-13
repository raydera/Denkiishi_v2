using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    [Table("kanji_meaning_mnemonic")]
    public class KanjiMeaningMnemonic
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("kanji_meaning_id")]
        public int KanjiMeaningId { get; set; }

        [Column("text")]
        public string Text { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Relacionamento (opcional por enquanto, mas bom ter)
        [ForeignKey("KanjiMeaningId")]
        public virtual KanjiMeaning KanjiMeaning { get; set; }
    }
}