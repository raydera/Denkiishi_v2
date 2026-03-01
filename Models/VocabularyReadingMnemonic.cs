using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    [Table("vocabulary_reading_mnemonic")] // <-- Corrigido com o "c"
    public class VocabularyReadingMnemonic
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("vocabulary_reading_id")] // <-- Corrigido com o "c"
        public int VocabularyReadingId { get; set; }

        [Column("text")]
        [Required]
        public string Text { get; set; }

        [Column("is_active")]
        public bool? IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}