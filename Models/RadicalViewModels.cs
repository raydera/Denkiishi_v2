using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Denkiishi_v2.Models
{
    // ViewModel para a tela Index (O Mapa de Radicais)
    public class RadicalGeralViewModel
    {
        public int LinguaSelecionadaId { get; set; }
        public List<SelectListItem> LinguasDisponiveis { get; set; } = new List<SelectListItem>();

        // AQUI ESTÁ A MUDANÇA: Dicionário de <Traços, Lista de Radicais>
        public Dictionary<int, List<RadicalStatusDto>> RadicaisPorTracos { get; set; } = new Dictionary<int, List<RadicalStatusDto>>();
    }

    // DTO para os botões da tela principal
    public class RadicalStatusDto
    {
        public int Id { get; set; }
        public string Literal { get; set; }
        public bool TemTraducao { get; set; }
        public string SearchText { get; set; }
    }

    // ViewModel para o Modal (Detalhes do Radical)
    public class RadicalDetalhesViewModel
    {
        public int RadicalId { get; set; } // Nome correto
        public string Caractere { get; set; }
        public string InfoTraços { get; set; } // Em vez de LeituraOnyomi

        // Para o CRUD
        public int LinguaSelecionadaId { get; set; }
        public List<SelectListItem> LinguasDisponiveis { get; set; } = new List<SelectListItem>();
        public List<GrupoTraducaoDto> TraducoesAgrupadas { get; set; } = new List<GrupoTraducaoDto>();
    }
}