using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    [Table("radical_meaning_mnemonic")]
    public class RadicalMeaningMnemonic
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("radical_meaning_id")]
        public int RadicalMeaningId { get; set; }

        [Column("text")]
        public string Text { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("RadicalMeaningId")]
        public virtual RadicalMeaning RadicalMeaning { get; set; }
    }
}