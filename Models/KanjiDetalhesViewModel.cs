using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Denkiishi_v2.Models
{
    public class KanjiDetalhesViewModel
    {
        public int KanjiId { get; set; }
        public string Caractere { get; set; }

        public string LeituraOnyomi { get; set; }

        // --- NOVO: Lista de Categorias (JLPT, Wanikani, etc) ---
        public List<CategoriaDto> Categorias { get; set; } = new List<CategoriaDto>();

        // Lista de Kanjis similares
        public List<KanjiSimilarDto> KanjisSimilares { get; set; } = new List<KanjiSimilarDto>();

        // Radicais com descrição
        public List<RadicalDto> Radicais { get; set; } = new List<RadicalDto>();

        // Traduções agrupadas por idioma
        public List<GrupoTraducaoDto> TraducoesAgrupadas { get; set; } = new List<GrupoTraducaoDto>();
        public int LinguaSelecionadaId { get; set; }
        public List<SelectListItem> LinguasDisponiveis { get; set; } = new List<SelectListItem>();
        public string NovasPalavrasJson { get; set; }
        public bool TemHistoria { get; set; }
    }

    // --- NOVO: DTO para transportar os dados da categoria ---
    public class CategoriaDto
    {
        public string Nome { get; set; }  // Ex: "JLPT"
        public string Valor { get; set; } // Ex: "N2"
    }

    public class KanjiSimilarDto
    {
        public int Id { get; set; }
        public string Caractere { get; set; }
        public string Nome { get; set; }
    }

    public class RadicalDto
    {
        public string Caractere { get; set; }
        public string Descricao { get; set; }
    }

    public class GrupoTraducaoDto
    {
        public string Lingua { get; set; }
        public List<SignificadoDto> Significados { get; set; } = new List<SignificadoDto>();
    }

    public class SignificadoDto
    {
        public int Id { get; set; }
        public string Texto { get; set; }
        public bool IsPrincipal { get; set; }
    }

    public class SalvarTraducaoRequest
    {
        public int KanjiId { get; set; }
        public int LinguaId { get; set; }
        public List<PalavraItem> Palavras { get; set; } = new List<PalavraItem>();
    }

    public class PalavraItem
    {
        public string Texto { get; set; }
        public bool EhPrincipal { get; set; }
    }
}