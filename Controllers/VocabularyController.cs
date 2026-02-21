using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Denkiishi_v2.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Denkiishi_v2.Controllers
{
    [Authorize(Roles = "Admin")]
    public class VocabularyController : Controller
    {
        private readonly InasDbContext _context;

        public VocabularyController(InasDbContext context)
        {
            _context = context;
        }

        // =======================================================
        // MÉTODOS MOCKADOS PARA VALIDAR O VISUAL (VERDE MENTA)
        // =======================================================

        // GET: /Vocabulary/Index
        // GET: /Vocabulary/Index
        public async Task<IActionResult> Index(int? linguaId, int? categoriaId)
        {
            // 1. Configurar Língua Padrão (Português)
            var linguaPadrao = await _context.Language.FirstOrDefaultAsync(l => l.LanguageCode == "pt-br")
                            ?? await _context.Language.FirstOrDefaultAsync();
            int idLinguaFinal = linguaId ?? linguaPadrao?.Id ?? 1;

            // 2. Configurar Categoria Padrão (Ex: JLPT)
            var categoriaPadrao = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "JLPT")
                               ?? await _context.Categories.FirstOrDefaultAsync();
            int idCategoriaFinal = categoriaId ?? categoriaPadrao?.Id ?? 1;

            // ====================================================================
            // 3. QUERY REESCRITA (Buscando em memória para evitar erro de tradução SQL)
            // ====================================================================

            // Passo A: Busca apenas a relação básica (Vocabulary + CategoryMap)
            var relacoes = await _context.VocabularyCategoryMaps
                .Where(vcm => vcm.CategoryId == idCategoriaFinal)
                .Include(vcm => vcm.Vocabulary) // Garante que traga os dados da tabela Vocabulary
                .ToListAsync();

            // Passo B: Extrai os IDs dos vocabulários para buscar os detalhes
            var idsVocabularios = relacoes.Select(r => r.VocabularyId).Distinct().ToList();

            // Passo C: Busca as leituras primárias
            var leituras = await _context.VocabularyReadings
                 .Where(r => idsVocabularios.Contains(r.VocabularyId))
                 .ToListAsync();

            // Passo D: Busca os significados na língua selecionada
            var significados = await _context.VocabularyMeanings
                 .Where(m => idsVocabularios.Contains(m.VocabularyId) && m.LanguageId == idLinguaFinal)
                 .ToListAsync();

            // ====================================================================

            // 4. Montar a ViewModel com Agrupamento usando os dados em memória
            var model = new VocabularyGeralViewModel
            {
                LinguaSelecionadaId = idLinguaFinal,
                CategoriaSelecionadaId = idCategoriaFinal
            };

            var listaDtos = new List<VocabularyStatusDto>();

            foreach (var rel in relacoes)
            {
                if (rel.Vocabulary == null) continue; // Evita NullReferenceException

                var vId = rel.Vocabulary.Id;

                // Encontra a leitura primária (ou a primeira que achar)
                var leituraPrimaria = leituras
                    .Where(r => r.VocabularyId == vId)
                    .OrderByDescending(r => r.IsPrimary)
                    .Select(r => r.Reading)
                    .FirstOrDefault();

                // Pega os significados desta palavra
                var significadosDaPalavra = significados
                    .Where(m => m.VocabularyId == vId)
                    .Select(m => m.Meaning)
                    .ToList();

                listaDtos.Add(new VocabularyStatusDto
                {
                    Id = vId,
                    Palavra = rel.Vocabulary.Characters,
                    LeituraPrincipal = leituraPrimaria ?? "",
                    TemTraducao = significadosDaPalavra.Any(),
                    SearchText = $"{rel.Vocabulary.Characters} {leituraPrimaria} {string.Join(" ", significadosDaPalavra)}".ToLower()
                });
            }

            // Agrupa os DTOs preenchidos
            model.VocabPorNivel = relacoes
                .GroupBy(r => string.IsNullOrEmpty(r.CategoryLevel) ? "Outros" : r.CategoryLevel)
                .OrderByDescending(g => int.TryParse(g.Key, out int num) ? num : -1)
                .ToDictionary(
                    g => g.Key,
                    g => listaDtos.Where(dto => g.Select(r => r.Vocabulary.Id).Contains(dto.Id)).ToList()
                );

            // 5. Preencher os Dropdowns da Tela
            model.LinguasDisponiveis = await _context.Language.OrderBy(l => l.Description)
                .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Description, Selected = l.Id == idLinguaFinal })
                .ToListAsync();

            model.CategoriasDisponiveis = await _context.Categories.OrderBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name, Selected = c.Id == idCategoriaFinal })
                .ToListAsync();

            return View(model);
        }
        public IActionResult DetalhesModal(int? id)
        {
            if (id == null) return NotFound();

            // MOCK PARA VALIDAR O LAYOUT DO MODAL
            var model = new VocabularyDetalhesViewModel
            {
                Id = id.Value,
                Palavra = "悪い",
                Nivel = "5", // Passado como string
                Leituras = new List<string> { "わるい" },
                ClassesGramaticais = new List<string> { "Adjetivo (i)" },

                KanjisComponentes = new List<KanjiComponenteDto>
                {
                    new KanjiComponenteDto { Id = 1, Caractere = "悪", Significado = "Ruim / Mal", Nivel = 4 }
                },

                Sentencas = new List<SentencaDto>
                {
                    new SentencaDto { Id = 1, Japones = "今日は天気が悪いですね。", Traducao = "O tempo está ruim hoje, não é?" }
                },

                TraducoesAgrupadas = new List<VocabGrupoTraducaoDto>
                {
                    new VocabGrupoTraducaoDto
                    {
                        Lingua = "Português (Brasil)",
                        Significados = new List<VocabSignificadoDto>
                        {
                            new VocabSignificadoDto { Id = 1, Texto = "Ruim", IsPrimary = true },
                            new VocabSignificadoDto { Id = 2, Texto = "Mal", IsPrimary = false }
                        }
                    }
                }
            };

            return PartialView("_ModalContent", model);
        }

        // =======================================================
        // SEUS MÉTODOS ANTIGOS DO VOCABULARY (INTACTOS)
        // =======================================================

        public async Task<IActionResult> Edit(int id)
        {
            var vocabulary = await _context.Vocabularies.FirstOrDefaultAsync(v => v.Id == id);
            if (vocabulary == null) return NotFound();

            var viewModel = new VocabularyEditViewModel
            {
                Id = vocabulary.Id,
                Characters = vocabulary.Characters,
                Level = (int)(vocabulary.Level ?? 0),
                AvailableLanguages = await _context.Language.ToListAsync(),

                ExistingMeanings = await _context.VocabularyMeanings
                    .Where(m => m.VocabularyId == id)
                    .Include(m => m.Language)
                    .ToListAsync(),

                ExistingSentences = await _context.VocabularyContextSentences
                    .Where(s => s.VocabularyId == id)
                    .Include(s => s.Language)
                    .ToListAsync(),

                KanjiComponents = await (from vc in _context.VocabularyCompositions
                                         join k in _context.Kanjis on vc.KanjiId equals k.Id
                                         where vc.VocabularyId == id
                                         select k).ToListAsync()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AddSentence(int vocabularyId, int languageId, string textJp, string textTranslated)
        {
            var newSentence = new VocabularyContextSentence
            {
                VocabularyId = vocabularyId,
                LanguageId = languageId,
                Jp = textJp,
                En = textTranslated
            };
            _context.VocabularyContextSentences.Add(newSentence);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = vocabularyId });
        }

        [HttpPost]
        public async Task<IActionResult> AddMeaning(int vocabularyId, int languageId, string meaning)
        {
            var newMeaning = new VocabularyMeaning
            {
                VocabularyId = vocabularyId,
                LanguageId = languageId,
                Meaning = meaning,
                Type = "primary"
            };
            _context.VocabularyMeanings.Add(newMeaning);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = vocabularyId });
        }
    }
}