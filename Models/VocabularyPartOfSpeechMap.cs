using System.ComponentModel.DataAnnotations.Schema;

namespace Denkiishi_v2.Models
{
    [Table("vocabulary_parts_speach_map")] // Mantendo o nome exato do seu SQL
    public class VocabularyPartOfSpeechMap
    {
        [Column("vocabulary_parts_speech_id")]
        public int VocabularyPartOfSpeechId { get; set; }

        public VocabularyPartOfSpeech PartOfSpeech { get; set; }

        [Column("vocabulary_id")]
        public int VocabularyId { get; set; }

        public Vocabulary Vocabulary { get; set; }
    }
}