using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    [Table("syntax_highlight")]
    public class SyntaxHighlight
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("code")]
        [StringLength(20)]
        public string Code { get; set; } // Ex: "v", "222"

        [Required]
        [Column("description")]
        [StringLength(100)]
        public string Description { get; set; }

        // --- ESTILOS VISUAIS ---

        [Column("text_color")]
        [StringLength(7)]
        public string TextColor { get; set; } // Ex: #RRGGBB

        [Column("background_color")]
        [StringLength(7)]
        public string BackgroundColor { get; set; }

        [Column("is_bold")]
        public bool IsBold { get; set; }

        [Column("is_italic")]
        public bool IsItalic { get; set; }

        [Column("is_underline")]
        public bool IsUnderline { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}