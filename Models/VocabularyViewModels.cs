using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Denkiishi_v2.Models
{
    // === VIEW MODEL DA TELA INDEX ===
    public class VocabularyGeralViewModel
    {
        // Alterado de int para string, pois category_level é texto
        public Dictionary<string, List<VocabularyStatusDto>> VocabPorNivel { get; set; } = new();

        public int LinguaSelecionadaId { get; set; }
        public List<SelectListItem> LinguasDisponiveis { get; set; } = new();

        // NOVO: Filtro de Categoria
        public int CategoriaSelecionadaId { get; set; }
        public List<SelectListItem> CategoriasDisponiveis { get; set; } = new();
    }

    public class VocabularyStatusDto
    {
        public int Id { get; set; }
        public string Palavra { get; set; }
        public string LeituraPrincipal { get; set; }
        public bool TemTraducao { get; set; }
        public string SearchText { get; set; }
    }

    // === VIEW MODEL DO MODAL (Para os próximos passos) ===
    public class VocabularyDetalhesViewModel
    {
        public int Id { get; set; }
        public string Palavra { get; set; }
        public string Nivel { get; set; }
        public List<string> Leituras { get; set; } = new();
        public List<VocabGrupoTraducaoDto> TraducoesAgrupadas { get; set; } = new();
        public List<string> ClassesGramaticais { get; set; } = new();
        public List<KanjiDto> KanjisComponentes { get; set; } = new();
        public List<SentencaDto> Sentencas { get; set; } = new();
        public int LinguaSelecionadaId { get; set; }
        public List<SelectListItem> LinguasDisponiveis { get; set; } = new();
    }

    public class VocabGrupoTraducaoDto
    {
        public string Lingua { get; set; }
        public List<VocabSignificadoDto> Significados { get; set; } = new();
    }

    public class VocabSignificadoDto
    {
        public int Id { get; set; }
        public string Texto { get; set; }
        public bool IsPrimary { get; set; }
        public bool TemHistoria { get; set; }
    }

    public class SentencaDto
    {
        public int Id { get; set; }
        public string Japones { get; set; }
        public string Traducao { get; set; }
    }
}