using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Denkiishi_v2.Models
{
    public class VocabularyGeralViewModel
    {
        public Dictionary<string, List<VocabularyStatusDto>> VocabPorNivel { get; set; } = new();
        public int LinguaSelecionadaId { get; set; }
        public List<SelectListItem> LinguasDisponiveis { get; set; } = new();
        public int CategoriaSelecionadaId { get; set; }
        public List<SelectListItem> CategoriasDisponiveis { get; set; } = new();
    }

    public class VocabularyStatusDto
    {
        public int Id { get; set; }
        public string Palavra { get; set; }
        public string LeituraPrincipal { get; set; }
        public string SignificadoPrincipal { get; set; }
        public bool TemTraducao { get; set; }
        public string SearchText { get; set; }
        public string NivelCategoria { get; set; }
    }

    public class VocabularyDetalhesViewModel
    {
        public int Id { get; set; }
        public string Palavra { get; set; }
        public string Nivel { get; set; }
        public List<VocabReadingDto> ListaReadings { get; set; } = new List<VocabReadingDto>();
        public List<VocabGrupoTraducaoDto> TraducoesAgrupadas { get; set; } = new();
        public List<string> ClassesGramaticais { get; set; } = new();

        // CORRIGIDO AQUI: A classe correta para compor a View
        public List<KanjiComponenteDto> KanjisComponentes { get; set; } = new();

        public List<SentencaDto> Sentencas { get; set; } = new();
        public int LinguaSelecionadaId { get; set; }
        public List<SelectListItem> LinguasDisponiveis { get; set; } = new();
        public List<SyntaxHighlight> ListaSyntax { get; set; } = new();
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

    // CORRIGIDO AQUI: Adicionada a classe que faltava
    public class KanjiComponenteDto
    {
        public int Id { get; set; }
        public string Caractere { get; set; }
        public string Significado { get; set; }
        public int Nivel { get; set; }
    }

    public class SentencaDto
    {
        public int Id { get; set; }
        public string Japones { get; set; }
        public string Traducao { get; set; }
    }

    public class VocabReadingDto
    {
        public int Id { get; set; }
        public string Reading { get; set; }
        public bool IsPrimary { get; set; }
        public bool TemHistoria { get; set; }
    }
}