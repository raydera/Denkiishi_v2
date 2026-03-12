using System.Collections.Generic;

namespace Denkiishi_v2.Models
{
    public class LessonItemViewModel
    {
        public int Id { get; set; }
        public string Tipo { get; set; } // "radical", "kanji", "vocab"
        public string Caractere { get; set; }
        public string SignificadoPrincipal { get; set; }
        public string LeituraPrincipal { get; set; }
        public string MnemonicSignificado { get; set; }
        public string MnemonicLeitura { get; set; }

        public List<string> ClassesGramaticais { get; set; } = new List<string>();
        public List<RelatedKanjiDto> KanjisRelacionados { get; set; } = new List<RelatedKanjiDto>();

        // NOVAS PROPRIEDADES PARA O KANJI:
        public List<ItemTextDto> Significados { get; set; } = new List<ItemTextDto>();
        public List<ItemTextDto> Leituras { get; set; } = new List<ItemTextDto>();
        public List<RelatedVocabDto> VocabulariosRelacionados { get; set; } = new List<RelatedVocabDto>();
    }
    public class RelatedKanjiDto
    {
        public int Id { get; set; }
        public string Caractere { get; set; }
        public string Significado { get; set; }
    }
    public class LessonSessionViewModel
    {
        public int SessionId { get; set; }
        public List<LessonItemViewModel> Itens { get; set; } = new List<LessonItemViewModel>();
    }
    // ==========================================
    // MODELOS PARA O DASHBOARD (SELEÇÃO)
    // ==========================================
    public class LessonDashboardViewModel
    {
        public List<MandalaStudentDto> Mandalas { get; set; } = new List<MandalaStudentDto>();
    }

    public class MandalaStudentDto
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public int Ordem { get; set; }
        public List<CircleStudentDto> Circulos { get; set; } = new List<CircleStudentDto>();
    }

    public class CircleStudentDto
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public int Ordem { get; set; }
        public List<ItemStudentDto> Itens { get; set; } = new List<ItemStudentDto>();
    }

    public class ItemStudentDto
    {
        public int IdOriginal { get; set; }
        public string Tipo { get; set; } // "radical", "kanji", "vocab"
        public string Caractere { get; set; }
    }

    // 1. DTO para os Textos (Meanings e Readings)
    public class ItemTextDto
    {
        public string Texto { get; set; }
        public bool IsPrimary { get; set; }
    }

    // 2. DTO para Vocabulários Relacionados
    public class RelatedVocabDto
    {
        public int Id { get; set; }
        public string Caractere { get; set; }
        // Se quiser no futuro, podemos adicionar Significado aqui também
    }
}