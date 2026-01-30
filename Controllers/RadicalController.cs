using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Denkiishi_v2.Models;
using Microsoft.AspNetCore.Authorization;

namespace Denkiishi_v2.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RadicalController : Controller
    {
        private readonly InasDbContext _context;

        public RadicalController(InasDbContext context)
        {
            _context = context;
        }

        // GET: /Radical/Index
        public async Task<IActionResult> Index(int? linguaId)
        {
            // 1. Define Língua
            var linguaPadrao = await _context.Language
                .FirstOrDefaultAsync(l => l.LanguageCode == "pt-br")
                ?? await _context.Language.FirstOrDefaultAsync();

            int idLinguaFinal = linguaId ?? linguaPadrao?.Id ?? 1;

            // 2. Busca Radicais
            var listaRadicais = await _context.Radicals
                .OrderBy(r => r.StrokeCount)
                .Select(r => new
                {
                    r.Id,
                    r.Literal,
                    // CORREÇÃO DO ERRO DE COMPILAÇÃO: 
                    // Forçamos (int) porque StrokeCount é 'short?' no banco, 
                    // mas o Dictionary espera 'int'.
                    Traços = (int)(r.StrokeCount ?? 0),
                    TemTraducao = r.RadicalMeanings.Any(m => m.IdLanguage == idLinguaFinal),
                    Meanings = r.RadicalMeanings.Select(m => m.Description)
                })
                .ToListAsync();

            // 3. Monta ViewModel NOVO (RadicalGeralViewModel)
            var model = new RadicalGeralViewModel
            {
                LinguaSelecionadaId = idLinguaFinal,
                RadicaisPorTracos = listaRadicais
                    .GroupBy(r => r.Traços)
                    .OrderBy(g => g.Key)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => new RadicalStatusDto // Usando DTO novo
                        {
                            Id = x.Id,
                            Literal = x.Literal,
                            TemTraducao = x.TemTraducao,
                            SearchText = $"{x.Literal} {string.Join(" ", x.Meanings)}".ToLower()
                        }).ToList()
                    )
            };

            // Dropdown
            model.LinguasDisponiveis = await _context.Language
                .OrderBy(l => l.Description)
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = l.Description,
                    Selected = l.Id == idLinguaFinal
                }).ToListAsync();

            return View(model);
        }

        // GET: /Radical/DetalhesModal/5
        // GET: /Radical/DetalhesModal/5
        public async Task<IActionResult> DetalhesModal(int? id)
        {
            if (id == null) return NotFound();

            var radical = await _context.Radicals
                .Include(r => r.RadicalMeanings)
                    .ThenInclude(rm => rm.IdLanguageNavigation)
                // --- NOVO: Trazendo os Kanjis que usam este radical ---
                .Include(r => r.KanjiRadicals)
                    .ThenInclude(kr => kr.Kanji)
                // -----------------------------------------------------
                .FirstOrDefaultAsync(m => m.Id == id);

            if (radical == null) return NotFound();

            // ViewModel NOVO (RadicalDetalhesViewModel)
            // Se o seu RadicalDetalhesViewModel não tiver a propriedade 'KanjisRelacionados',
            // você precisará adicioná-la na classe ViewModel ou usar um objeto anônimo (dynamic) 
            // se preferir não mexer na classe agora. 
            // VOU USAR UM TRUQUE aqui: Passar via ViewBag para não quebrar seu modelo atual,
            // ou podemos criar um objeto anônimo novo.

            // Vamos criar um objeto novo para garantir que o Javascript receba tudo
            var resultadoParaTela = new
            {
                RadicalId = radical.Id,
                Caractere = radical.Literal,
                InfoTraços = $"{radical.StrokeCount} traços",

                // --- NOVO: Lista de Kanjis ---
                // --- NOVO: Lista de Kanjis (AGORA USANDO DTO PÚBLICO) ---
                KanjisRelacionados = radical.KanjiRadicals
    .Select(kr => new KanjiDto
    {
        Id = kr.Kanji.Id,
        Literal = kr.Kanji.Literal,
        // Convertendo para int e garantindo que se for nulo vira 0
        Nivel = (int)(kr.Kanji.JlptLevel ?? 0)
    })
    .Take(40)
    .OrderBy(k => k.Nivel)
    .ToList(),
                // -----------------------------
                // -----------------------------

                TraducoesAgrupadas = radical.RadicalMeanings
                    .Where(m => m.IdLanguageNavigation != null)
                    .GroupBy(m => m.IdLanguageNavigation.Description)
                    .Select(grupo => new GrupoTraducaoDto
                    {
                        Lingua = grupo.Key ?? "Indefinida",
                        Significados = grupo.Select(m => new SignificadoDto
                        {
                            Id = m.Id,
                            Texto = m.Description,
                            IsPrincipal = false
                        }).ToList()
                    }).ToList(),

                // Dropdown Línguas (Mantive igual)
                LinguasDisponiveis = (await _context.Language.OrderBy(l => l.Description).ToListAsync())
                    .Select(l => new SelectListItem
                    {
                        Value = l.Id.ToString(),
                        Text = l.Description,
                        Selected = (l.Description.Contains("Português") || l.LanguageCode == "pt-br")
                    }).ToList()
            };

            // Ajuste de seleção padrão do Dropdown
            var listaLinguas = resultadoParaTela.LinguasDisponiveis;
            int linguaSelecionadaId = 0;
            if (!listaLinguas.Any(x => x.Selected) && listaLinguas.Any())
            {
                listaLinguas.First().Selected = true;
                linguaSelecionadaId = int.Parse(listaLinguas.First().Value);
            }
            else if (listaLinguas.Any(x => x.Selected))
            {
                linguaSelecionadaId = int.Parse(listaLinguas.First(x => x.Selected).Value);
            }

            // ATENÇÃO: Como mudamos o tipo do objeto retornado (agora é anônimo com Kanjis),
            // se a sua View "_ModalContent" for fortemente tipada (@model RadicalDetalhesViewModel),
            // ela vai reclamar.

            // SOLUÇÃO: Vamos passar os Kanjis via ViewBag para a View, assim não mexemos no ViewModel agora.
            ViewBag.KanjisRelacionados = resultadoParaTela.KanjisRelacionados;

            // Recriamos o Model original para a View não quebrar
            var modelOriginal = new RadicalDetalhesViewModel
            {
                RadicalId = resultadoParaTela.RadicalId,
                Caractere = resultadoParaTela.Caractere,
                InfoTraços = resultadoParaTela.InfoTraços,
                TraducoesAgrupadas = resultadoParaTela.TraducoesAgrupadas,
                LinguasDisponiveis = resultadoParaTela.LinguasDisponiveis,
                LinguaSelecionadaId = linguaSelecionadaId
            };

            return PartialView("_ModalContent", modelOriginal);
        }
        // POST: Salvar
        [HttpPost]
        // POST: Salvar (AGORA COM AS MENSAGENS CERTAS! ❤️)
        [HttpPost]
        public async Task<IActionResult> SalvarTraducoes([FromBody] SalvarTraducaoRequest dados)
        {
            if (dados == null || dados.Palavras.Count == 0) return BadRequest("Nenhum dado.");

            try
            {
                var radicalExists = await _context.Radicals.AnyAsync(r => r.Id == dados.KanjiId);
                if (!radicalExists) return NotFound("Radical não encontrado.");

                foreach (var item in dados.Palavras)
                {
                    bool jaExiste = await _context.RadicalMeanings
                        .AnyAsync(m => m.IdRadical == dados.KanjiId
                                       && m.IdLanguage == dados.LinguaId
                                       && m.Description.ToLower() == item.Texto.ToLower());

                    if (jaExiste) return BadRequest($"A tradução '{item.Texto}' já existe para este idioma.");

                    var novaTraducao = new RadicalMeaning
                    {
                        IdRadical = dados.KanjiId,
                        IdLanguage = dados.LinguaId,
                        Description = item.Texto
                    };
                    _context.RadicalMeanings.Add(novaTraducao);
                }

                await _context.SaveChangesAsync();

                // --- AQUI ESTÃO ELAS DE VOLTA! ---
                var mensagensDivertidas = new List<string>
                {
                    "Yatta! Radical aprendido! 🎉",
                    "Sugoi! A base do Denkiishi ficou mais forte! 💪",
                    "Arigato! Sua sabedoria é incrível! 🙇‍♂️",
                    "Salvo! Você está dominando as raízes! ⚔️",
                    "Eita amor! Mandou bem demais! 🎨",
                    "Amor, você é incrível! Salvo! ❤️",
                    "Ei gatinho, mais um radical pra conta 😻",
                    "Vai com calma, radical salvo com sucesso! 🧘‍♂️",
                    "Orgulho de você! ❤️",
                    "Mais um degrau rumo ao Denkiishi! 🚀"
                };

                var random = new Random();
                return Ok(new { mensagem = mensagensDivertidas[random.Next(mensagensDivertidas.Count)] });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro: {ex.Message}");
            }
        }
        // POST: Excluir
        [HttpPost]
        public async Task<IActionResult> ExcluirTraducao(int id)
        {
            var traducao = await _context.RadicalMeanings.FindAsync(id);
            if (traducao == null) return NotFound("Tradução não encontrada.");

            // Guardamos os IDs antes de deletar
            int radicalId = traducao.IdRadical ?? 0;
            int linguaId = traducao.IdLanguage ?? 0;

            _context.RadicalMeanings.Remove(traducao);
            await _context.SaveChangesAsync();

            // VERIFICAÇÃO: Ainda existe alguma tradução para este radical NESTE idioma?
            bool aindaTemTraducao = await _context.RadicalMeanings
                .AnyAsync(m => m.IdRadical == radicalId && m.IdLanguage == linguaId);

            // Retornamos essa informação para o JavaScript
            return Ok(new { mensagem = "Removido!", temMais = aindaTemTraducao, id = radicalId });
        }
    }

    public class KanjiDto
    {
        public int Id { get; set; }
        public string Literal { get; set; }
        public int Nivel { get; set; }
    }
}