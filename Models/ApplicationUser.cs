using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Denkiishi_v2.Models
{
    // Herdamos de IdentityUser para ganhar nome, email, senha hash, etc.
    public class ApplicationUser : IdentityUser
    {
        // Aqui podemos adicionar campos extras no futuro.
        // Por exemplo: public string NivelJapones { get; set; }
        [StringLength(50)]
        public string Nickname { get; set; } // Nova propriedade
    }
}
