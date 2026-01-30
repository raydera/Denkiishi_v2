using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Denkiishi_v2.Models;
using Microsoft.AspNetCore.Authorization;
using System;

namespace Denkiishi_v2.Controllers
{
    [Authorize(Roles = "Admin")]
    public class KanjiController : Controller
    {
        private readonly InasDbContext _context;

        public KanjiController(InasDbContext context)
        {
            _context = context;
        }

        // ==========================================================
        // 1. INDEX (SEU CÓDIGO ATUAL - ESTÁ SEGURO E CORRETO)
        // ==========================================================
        public async Task<IActionResult> Index(int? categoriaId, int? linguaId)
        {
            var viewModel = new KanjiGeralViewModel();

            // A. Carregar Categorias
            var categoriasDb = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            viewModel.CategoriasDisponiveis = categoriasDb
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToList();

            if (categoriaId.HasValue) viewModel.CategoriaSelecionadaId = categoriaId.Value;
            else
            {
                var padrao = categoriasDb.FirstOrDefault(c => c.Name.Contains("JLPT")) ?? categoriasDb.FirstOrDefault();
                viewModel.CategoriaSelecionadaId = padrao?.Id ?? 0;
            }

            // B. Carregar Línguas (Sua lógica do "Pulo do Gato" mantida)
            var linguasDb = await _context.Language.Where(l => l.IsActive == true).ToListAsync();

            if (linguaId.HasValue) viewModel.LinguaSelecionadaId = linguaId.Value;
            else
            {
                var padrao = linguasDb.FirstOrDefault(l => l.Description.Contains("Português (pt-BR)")) ?? linguasDb.FirstOrDefault();
                viewModel.LinguaSelecionadaId = padrao?.Id ?? 0;
            }

            viewModel.LinguasDisponiveis = linguasDb
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = l.Description,
                    Selected = (l.Id == viewModel.LinguaSelecionadaId)
                })
                .ToList();

            // C. Buscar Kanjis
            if (viewModel.CategoriaSelecionadaId > 0)
            {
                var dados = await _context.KanjiCategoryMaps
                    .Include(kc => kc.Kanji)
                        .ThenInclude(k => k.KanjiMeanings)
                    .Where(kc => kc.CategoryId == viewModel.CategoriaSelecionadaId)
                    .Where(kc => kc.Kanji.IsActive == true)
                    .ToListAsync();

                // D. Agrupar por Nível
                viewModel.KanjisPorNivel = dados
                    .GroupBy(kc => kc.CategoryLevel)
                    .OrderBy(g => g.Key)
                    .ToDictionary(
                        g => g.Key ?? "Outros",
                        g => g.Select(m => new KanjiStatusDto
                        {
                            Id = m.Kanji.Id,
                            Literal = m.Kanji.Literal,
                            TemTraducao = m.Kanji.KanjiMeanings.Any(km => km.IdLanguage == viewModel.LinguaSelecionadaId),
                            SearchText = (m.Kanji.Literal + " " + string.Join(" ", m.Kanji.KanjiMeanings.Select(km => km.Gloss))).ToLower()
                        }).ToList()
                    );
            }

            return View(viewModel);
        }

        // ==========================================================
        // 2. DETALHES MODAL (Para o AJAX - Retorna PartialView)
        // ==========================================================
        [HttpGet]
        public async Task<IActionResult> DetalhesModal(int? id)
        {
            var model = await MontarViewModelDetalhes(id); // Usa método auxiliar para não repetir código
            if (model == null) return NotFound();

            return PartialView("_ModalContent", model);
        }

        // ==========================================================
        // 3. DETALHES (Para Tela Cheia - Retorna View Completa)
        // ==========================================================
        public async Task<IActionResult> Detalhes(int? id)
        {
            var model = await MontarViewModelDetalhes(id);
            if (model == null) return NotFound();

            // AQUI ESTÁ A CORREÇÃO: Retorna a View normal com Layout
            return View("Detalhes", model);
        }

        // ==========================================================
        // MÉTODO AUXILIAR (Evita duplicar código entre Modal e Detalhes)
        // ==========================================================
        private async Task<KanjiDetalhesViewModel> MontarViewModelDetalhes(int? id)
        {
            if (id == null) return null;

            var kanji = await _context.Kanjis
                .Include(k => k.KanjiMeanings).ThenInclude(km => km.IdLanguageNavigation)
                .Include(k => k.KanjiReadings)
                .Include(k => k.KanjiRadicals).ThenInclude(kr => kr.Radical).ThenInclude(r => r.RadicalMeanings).ThenInclude(rm => rm.IdLanguageNavigation)
                .Include(k => k.KanjiSimilarityIdKanjiNavigations).ThenInclude(s => s.IdKanjiSimilarNavigation).ThenInclude(ks => ks.KanjiMeanings)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (kanji == null) return null;

            var leiturasOnyomi = kanji.KanjiReadings.Where(r => r.Type == "onyomi" || r.Type == "ONYOMI").Select(r => r.ReadingKana).ToList();
            string textoLeitura = leiturasOnyomi.Any() ? string.Join(", ", leiturasOnyomi) : "-";

            var model = new KanjiDetalhesViewModel
            {
                KanjiId = kanji.Id,
                Caractere = kanji.Literal,
                LeituraOnyomi = textoLeitura,
                Radicais = kanji.KanjiRadicals.Select(kr => {
                    var r = kr.Radical;
                    if (r == null) return null;
                    var desc = r.RadicalMeanings.FirstOrDefault(rm => rm.IdLanguageNavigation != null && rm.IdLanguageNavigation.LanguageCode == "pt-br")?.Description
                               ?? r.RadicalMeanings.FirstOrDefault()?.Description ?? "Sem descrição";
                    return new RadicalDto { Caractere = r.Literal ?? "?", Descricao = desc };
                }).Where(x => x != null).ToList(),
                KanjisSimilares = kanji.KanjiSimilarityIdKanjiNavigations.Select(s => {
                    var sim = s.IdKanjiSimilarNavigation;
                    if (sim == null) return null;
                    var sig = sim.KanjiMeanings?.FirstOrDefault(m => m.IsPrincipal == true)?.Gloss ?? "Sem tradução";
                    return new KanjiSimilarDto { Id = sim.Id, Caractere = sim.Literal, Nome = sig };
                }).Where(x => x != null).ToList(),
                TraducoesAgrupadas = kanji.KanjiMeanings
                    .Where(m => m.IdLanguageNavigation != null)
                    .GroupBy(m => m.IdLanguageNavigation.Description)
                    .Select(g => new GrupoTraducaoDto
                    {
                        Lingua = g.Key ?? "Indefinida",
                        Significados = g.Select(m => new SignificadoDto { Id = m.Id, Texto = m.Gloss, IsPrincipal = m.IsPrincipal ?? false }).ToList()
                    }).ToList()
            };

            var linguasDb = await _context.Language.Where(l => l.IsActive == true).OrderBy(l => l.Description).ToListAsync();
            model.LinguasDisponiveis = linguasDb.Select(l => new SelectListItem
            {
                Value = l.Id.ToString(),
                Text = l.Description,
                Selected = l.LanguageCode == "pt-br"
            }).ToList();

            var sel = model.LinguasDisponiveis.FirstOrDefault(x => x.Selected) ?? model.LinguasDisponiveis.FirstOrDefault();
            if (sel != null) model.LinguaSelecionadaId = int.Parse(sel.Value);

            return model;
        }

        // ==========================================================
        // 4. MÉTODOS DE SALVAR E EXCLUIR (Mantidos)
        // ==========================================================
        [HttpPost]
        public async Task<IActionResult> SalvarTraducoes([FromBody] SalvarTraducaoRequest dados)
        {
            if (dados == null || dados.Palavras.Count == 0) return BadRequest("Nenhum dado recebido.");

            try
            {
                foreach (var item in dados.Palavras)
                {
                    bool jaExiste = await _context.KanjiMeanings.AnyAsync(m => m.KanjiId == dados.KanjiId && m.IdLanguage == dados.LinguaId && m.Gloss.ToLower() == item.Texto.ToLower());
                    if (jaExiste) return BadRequest($"A palavra '{item.Texto}' já está cadastrada.");

                    if (item.EhPrincipal)
                    {
                        var anterior = await _context.KanjiMeanings.FirstOrDefaultAsync(m => m.KanjiId == dados.KanjiId && m.IdLanguage == dados.LinguaId && m.IsPrincipal == true);
                        if (anterior != null) anterior.IsPrincipal = false;
                    }

                    var nova = new KanjiMeaning { KanjiId = dados.KanjiId, IdLanguage = dados.LinguaId, Gloss = item.Texto, IsPrincipal = item.EhPrincipal };
                    _context.KanjiMeanings.Add(nova);
                }
                await _context.SaveChangesAsync();

                var msgs = new List<string> { "Salvo com sucesso!", "Boa! Mais uma tradução.", "Excelente contribuição!" };
                return Ok(new { mensagem = msgs[new Random().Next(msgs.Count)] });
            }
            catch (Exception ex) { return StatusCode(500, $"Erro: {ex.Message}"); }
        }

        [HttpPost]
        public async Task<IActionResult> ExcluirTraducao(int id)
        {
            var t = await _context.KanjiMeanings.FindAsync(id);
            if (t == null) return NotFound("Tradução não encontrada.");

            int kId = t.KanjiId;
            int lId = t.IdLanguage ?? 0;
            _context.KanjiMeanings.Remove(t);
            await _context.SaveChangesAsync();

            bool temMais = await _context.KanjiMeanings.AnyAsync(m => m.KanjiId == kId && m.IdLanguage == lId);
            return Ok(new { mensagem = "Removido!", temMais = temMais, id = kId });
        }
    }
}