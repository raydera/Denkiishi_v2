using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    [Table("user")] // Mapeia para a tabela 'user' do SQL
    public class UserCustom
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("username")]
        public string Username { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("registered_at")]
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        [Column("birth_date")]
        public DateTime? BirthDate { get; set; }

        // Relacionamentos (Navegação)
        public ICollection<Deck> Decks { get; set; }
        public ICollection<UserProgress> Progresses { get; set; }
        public ICollection<ReviewHistory> Reviews { get; set; }
    }
}