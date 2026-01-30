using Microsoft.AspNetCore.Mvc.Rendering;

namespace Denkiishi_v2.Models
{
    public class KanjiGeralViewModel
    {
        // --- PARTE EXISTENTE (Mantida) ---
        public int LinguaSelecionadaId { get; set; }
        public List<SelectListItem> LinguasDisponiveis { get; set; } = new List<SelectListItem>();

        // --- PARTE NOVA (Categorias) ---
        public int CategoriaSelecionadaId { get; set; }
        public List<SelectListItem> CategoriasDisponiveis { get; set; } = new List<SelectListItem>();

        // --- ALTERAÇÃO IMPORTANTE ---
        // Mudamos de Dictionary<int, ...> para Dictionary<string, ...>
        // Motivo: O nível agora é texto ("N1", "Grade 3", "N5") e não só número.
        public Dictionary<string, List<KanjiStatusDto>> KanjisPorNivel { get; set; } = new Dictionary<string, List<KanjiStatusDto>>();
    }

    public class KanjiStatusDto
    {
        public int Id { get; set; }
        public string Literal { get; set; }
        public bool TemTraducao { get; set; }
        public string SearchText { get; set; }
    }
}