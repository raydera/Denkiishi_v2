using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    [Table("vocabulary_parts_speech")]
    public class VocabularyPartOfSpeech
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        [Required]
        public string Name { get; set; }

        // Navegação para a tabela de mapa
        public ICollection<VocabularyPartOfSpeechMap> VocabularyMaps { get; set; }
    }
}