using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    [Table("vocabulary_meaning_mnemonic")]
    public class VocabularyMeaningMnemonic
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("vocabulary_meaning_id")]
        public int VocabularyMeaningId { get; set; }

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