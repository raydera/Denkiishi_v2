using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Denkiishi_v2.Models
{
    public class RadicalGeralViewModel
    {
        public Dictionary<int, List<RadicalStatusDto>> RadicaisPorTracos { get; set; }
        public int LinguaSelecionadaId { get; set; }
        public List<SelectListItem> LinguasDisponiveis { get; set; }
        public int CategoriaSelecionadaId { get; set; }
    }

    public class RadicalStatusDto
    {
        public int Id { get; set; }
        public string Literal { get; set; }
        public bool TemTraducao { get; set; }
        public string SearchText { get; set; }
    }

    // --- VIEWMODEL DO MODAL ---
    public class RadicalDetalhesViewModel
    {
        public int RadicalId { get; set; }
        public string Caractere { get; set; }
        public string InfoTraços { get; set; }

        public List<RadicalGrupoTraducaoDto> TraducoesAgrupadas { get; set; } = new();
        public List<SelectListItem> LinguasDisponiveis { get; set; } = new();
        public int LinguaSelecionadaId { get; set; }

        public List<KanjiDto> KanjisRelacionados { get; set; } = new();
        public List<SyntaxHighlight> ListaSyntax { get; set; } = new();
    }

    public class RadicalGrupoTraducaoDto
    {
        public string Lingua { get; set; }
        public List<RadicalSignificadoDto> Significados { get; set; }
    }

    public class RadicalSignificadoDto
    {
        public int Id { get; set; }
        public string Texto { get; set; }
        public bool IsPrincipal { get; set; }
        public bool TemHistoria { get; set; }
    }

    // --- ADICIONE ESTA CLASSE AQUI ---
    public class KanjiDto
    {
        public int Id { get; set; }
        public string Literal { get; set; }
        public int Nivel { get; set; }
    }
}