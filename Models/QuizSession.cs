using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    [Table("quiz_sessions")]
    public class QuizSession
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("user_id")]
        public string UserId { get; set; }

        [Required]
        [Column("current_state", TypeName = "jsonb")]
        public string CurrentState { get; set; } // Armazenaremos como string JSON para o EF Core

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
}