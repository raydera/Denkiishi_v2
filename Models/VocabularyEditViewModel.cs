namespace Denkiishi_v2.Models
{
    public class VocabularyEditViewModel
    {
        public int Id { get; set; }
        public string Characters { get; set; }
        public int Level { get; set; }
        public string MeaningsCsv { get; set; } // Facilitar exibição rápida

        // Lista de Kanjis que compõem este vocabulário
        public List<Kanji> KanjiComponents { get; set; } = new List<Kanji>();

        // Significados em diferentes línguas
        public List<VocabularyMeaning> ExistingMeanings { get; set; } = new List<VocabularyMeaning>();

        // Frases de contexto já traduzidas
        public List<VocabularyContextSentence> ExistingSentences { get; set; } = new List<VocabularyContextSentence>();

        // Listas para os Dropdowns da tela
        public List<Language> AvailableLanguages { get; set; }
    }
}