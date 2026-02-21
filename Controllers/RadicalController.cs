using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Denkiishi_v2.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

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
            var linguaPadrao = await _context.Language
                .FirstOrDefaultAsync(l => l.LanguageCode == "pt-br")
                ?? await _context.Language.FirstOrDefaultAsync();

            int idLinguaFinal = linguaId ?? linguaPadrao?.Id ?? 1;

            var listaRadicais = await _context.Radicals
                .OrderBy(r => r.StrokeCount)
                .Select(r => new
                {
                    r.Id,
                    r.Literal,
                    Traços = (int)(r.StrokeCount ?? 0),
                    TemTraducao = r.RadicalMeanings.Any(m => m.IdLanguage == idLinguaFinal),
                    Meanings = r.RadicalMeanings.Select(m => m.Description)
                })
                .ToListAsync();

            var model = new RadicalGeralViewModel
            {
                LinguaSelecionadaId = idLinguaFinal,
                RadicaisPorTracos = listaRadicais
                    .GroupBy(r => r.Traços)
                    .OrderBy(g => g.Key)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => new RadicalStatusDto
                        {
                            Id = x.Id,
                            Literal = x.Literal,
                            TemTraducao = x.TemTraducao,
                            SearchText = $"{x.Literal} {string.Join(" ", x.Meanings)}".ToLower()
                        }).ToList()
                    )
            };

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
        public async Task<IActionResult> DetalhesModal(int? id)
        {
            if (id == null) return NotFound();

            var radical = await _context.Radicals
                .Include(r => r.RadicalMeanings).ThenInclude(rm => rm.IdLanguageNavigation)
                .Include(r => r.KanjiRadicals).ThenInclude(kr => kr.Kanji)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (radical == null) return NotFound();

            // 1. Identifica histórias ativas
            var idsMeanings = radical.RadicalMeanings.Select(m => m.Id).ToList();
            var meaningsComHistoria = await _context.RadicalMeaningMnemonics
                .Where(m => idsMeanings.Contains(m.RadicalMeaningId) && m.IsActive)
                .Select(m => m.RadicalMeaningId)
                .ToListAsync();

            // 2. Monta o ViewModel
            var model = new RadicalDetalhesViewModel
            {
                RadicalId = radical.Id,
                Caractere = radical.Literal,
                InfoTraços = $"{radical.StrokeCount} traços",

                // Preenche a Lista de Syntax para a Toolbar
                ListaSyntax = await _context.SyntaxHighlights.AsNoTracking().OrderBy(x => x.Code).ToListAsync(),

                // Preenche os Kanjis Relacionados
                KanjisRelacionados = radical.KanjiRadicals
                    .Select(kr => new KanjiDto
                    {
                        Id = kr.Kanji.Id,
                        Literal = kr.Kanji.Literal,
                        Nivel = (int)(kr.Kanji.JlptLevel ?? 0)
                    })
                    .OrderBy(k => k.Nivel)
                    .Take(40)
                    .ToList(),

                TraducoesAgrupadas = radical.RadicalMeanings
                    .Where(m => m.IdLanguageNavigation != null)
                    .GroupBy(m => m.IdLanguageNavigation.Description)
                    .Select(g => new RadicalGrupoTraducaoDto
                    {
                        Lingua = g.Key ?? "Indefinida",
                        Significados = g.Select(m => new RadicalSignificadoDto
                        {
                            Id = m.Id,
                            Texto = m.Description,
                            IsPrincipal = false,
                            TemHistoria = meaningsComHistoria.Contains(m.Id)
                        }).ToList()
                    }).ToList()
            };

            // Dropdown de línguas
            var linguasDb = await _context.Language.OrderBy(l => l.Description).ToListAsync();
            model.LinguasDisponiveis = linguasDb.Select(l => new SelectListItem
            {
                Value = l.Id.ToString(),
                Text = l.Description,
                Selected = l.LanguageCode == "pt-br"
            }).ToList();

            var sel = model.LinguasDisponiveis.FirstOrDefault(x => x.Selected) ?? model.LinguasDisponiveis.FirstOrDefault();
            if (sel != null) model.LinguaSelecionadaId = int.Parse(sel.Value);

            return PartialView("_ModalContent", model);
        }

        // ==========================================
        // MÉTODOS AJAX DE HISTÓRIA
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> SalvarHistoriaMeaning([FromBody] SalvarHistoriaRequest request)
        {
            if (request == null || request.MeaningId <= 0) return BadRequest("Dados inválidos.");
            if (string.IsNullOrWhiteSpace(request.Texto)) return BadRequest("Texto vazio.");

            try
            {
                // Versionamento
                var antigas = await _context.RadicalMeaningMnemonics
                    .Where(m => m.RadicalMeaningId == request.MeaningId && m.IsActive)
                    .ToListAsync();

                foreach (var h in antigas) { h.IsActive = false; h.UpdatedAt = DateTime.UtcNow; }

                _context.RadicalMeaningMnemonics.Add(new RadicalMeaningMnemonic
                {
                    RadicalMeaningId = request.MeaningId,
                    Text = request.Texto,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                return Ok(new { mensagem = "História salva!" });
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpGet]
        public async Task<IActionResult> GetHistoria(int meaningId)
        {
            var txt = await _context.RadicalMeaningMnemonics
                .AsNoTracking()
                .Where(m => m.RadicalMeaningId == meaningId && m.IsActive)
                .Select(m => m.Text)
                .FirstOrDefaultAsync();
            return Ok(new { texto = txt ?? "" });
        }

        [HttpPost]
        public async Task<IActionResult> ExcluirHistoriaMeaning(int meaningId)
        {
            try
            {
                var ativas = await _context.RadicalMeaningMnemonics
                    .Where(m => m.RadicalMeaningId == meaningId && m.IsActive)
                    .ToListAsync();

                if (!ativas.Any()) return NotFound("História não encontrada.");

                foreach (var h in ativas) { h.IsActive = false; h.UpdatedAt = DateTime.UtcNow; }
                await _context.SaveChangesAsync();
                return Ok(new { mensagem = "Removida." });
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // ==========================================
        // MÉTODOS ANTIGOS DE TRADUÇÃO
        // ==========================================

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

                    if (jaExiste) continue;

                    _context.RadicalMeanings.Add(new RadicalMeaning
                    {
                        IdRadical = dados.KanjiId,
                        IdLanguage = dados.LinguaId,
                        Description = item.Texto
                    });
                }

                await _context.SaveChangesAsync();
                return Ok(new { mensagem = "Tradução salva!" });
            }
            catch (Exception ex) { return StatusCode(500, $"Erro: {ex.Message}"); }
        }

        [HttpPost]
        public async Task<IActionResult> ExcluirTraducao(int id)
        {
            var traducao = await _context.RadicalMeanings.FindAsync(id);
            if (traducao == null) return NotFound("Tradução não encontrada.");

            int radicalId = traducao.IdRadical ?? 0;
            int linguaId = traducao.IdLanguage ?? 0;

            _context.RadicalMeanings.Remove(traducao);
            await _context.SaveChangesAsync();

            bool temMais = await _context.RadicalMeanings
                .AnyAsync(m => m.IdRadical == radicalId && m.IdLanguage == linguaId);

            return Ok(new { mensagem = "Removido!", temMais = temMais, id = radicalId });
        }

        // Classes DTO Auxiliares
      
        public class SalvarHistoriaRequest { public int MeaningId { get; set; } public string Texto { get; set; } }
    }
}