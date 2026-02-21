using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    [Table("kanji_reading_mnemonic")]
    public class KanjiReadingMnemonic
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("kanji_reading_id")]
        public int KanjiReadingId { get; set; }

        [Column("text")]
        public string Text { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("KanjiReadingId")]
        public virtual KanjiReading KanjiReading { get; set; }
    }
}