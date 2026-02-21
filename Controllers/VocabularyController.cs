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

        // ==========================================
        // 1. NOVOS MÉTODOS (VISUALIZAÇÃO / MOCK)
        // ==========================================

        public async Task<IActionResult> Index(int? linguaId)
        {
            var model = new VocabularyGeralViewModel();

            // Mock de dados para visualizarmos o layout
            model.VocabPorNivel = new Dictionary<int, List<VocabularyStatusDto>>();

            model.VocabPorNivel.Add(1, new List<VocabularyStatusDto>
            {
                new VocabularyStatusDto { Id = 1, Palavra = "大", LeituraPrincipal = "だい", SignificadoPrincipal = "Grande", TemTraducao = true, SearchText = "dai grande" },
                new VocabularyStatusDto { Id = 2, Palavra = "一人", LeituraPrincipal = "ひとり", SignificadoPrincipal = "Uma pessoa", TemTraducao = true, SearchText = "hitori uma pessoa" },
                new VocabularyStatusDto { Id = 3, Palavra = "日本人", LeituraPrincipal = "にほんじん", SignificadoPrincipal = "Japonês (pessoa)", TemTraducao = false, SearchText = "nihonjin japones" }
            });

            model.LinguasDisponiveis = await _context.Language
                .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Description })
                .ToListAsync();

            return View(model);
        }

        public async Task<IActionResult> DetalhesModal(int? id)
        {
            if (id == null) return NotFound();

            // MOCK PARA VALIDAR O LAYOUT DO MODAL
            var model = new VocabularyDetalhesViewModel
            {
                Id = id.Value,
                Palavra = "日本人",
                Nivel = 2,
                Leituras = new List<string> { "にほんじん" },
                ClassesGramaticais = new List<string> { "Substantivo", "Sufixo" },

                KanjisComponentes = new List<KanjiComponenteDto>
                {
                    new KanjiComponenteDto { Id = 10, Caractere = "日", Significado = "Sol", Nivel = 1 },
                    new KanjiComponenteDto { Id = 11, Caractere = "本", Significado = "Livro/Origem", Nivel = 2 },
                    new KanjiComponenteDto { Id = 12, Caractere = "人", Significado = "Pessoa", Nivel = 1 }
                },

                Sentencas = new List<SentencaDto>
                {
                    new SentencaDto { Id = 1, Japones = "私は日本人です。", Traducao = "Eu sou japonês." },
                    new SentencaDto { Id = 2, Japones = "あの人は日本人ではありません。", Traducao = "Aquela pessoa não é japonesa." }
                },

                TraducoesAgrupadas = new List<VocabGrupoTraducaoDto>
                {
                    new VocabGrupoTraducaoDto
                    {
                        Lingua = "Português (Brasil)",
                        Significados = new List<VocabSignificadoDto>
                        {
                            new VocabSignificadoDto { Id = 1, Texto = "Japonês (pessoa)", IsPrimary = true },
                            new VocabSignificadoDto { Id = 2, Texto = "Cidadão Japonês", IsPrimary = false }
                        }
                    }
                }
            };

            return PartialView("_ModalContent", model);
        }

        // ==========================================
        // 2. SEUS MÉTODOS ANTIGOS MANTIDOS
        // ==========================================

        public async Task<IActionResult> Edit(int id)
        {
            var vocabulary = await _context.Vocabularies
                .FirstOrDefaultAsync(v => v.Id == id);

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

            // 3. A QUERY PRINCIPAL (Com os Joins conforme seu SQL)
            var vocabQuery = await (from voca in _context.Vocabularies
                                    join vcm in _context.VocabularyCategoryMaps on voca.Id equals vcm.VocabularyId
                                    where vcm.CategoryId == idCategoriaFinal
                                    select new
                                    {
                                        voca.Id,
                                        Palavra = voca.Characters,
                                        NivelCategoria = vcm.CategoryLevel,

                                        // Busca a leitura primária direto na query para performance
                                        Leitura = _context.VocabularyReadings
                                                    .Where(r => r.VocabularyId == voca.Id)
                                                    .OrderByDescending(r => r.IsPrimary) // Garante que a principal venha primeiro
                                                    .Select(r => r.Reading)
                                                    .FirstOrDefault(),

                                        // Verifica se tem tradução na língua selecionada
                                        TemTraducao = _context.VocabularyMeanings
                                                    .Any(m => m.VocabularyId == voca.Id && m.IdLanguage == idLinguaFinal),

                                        // Busca todos os significados para compor o texto de busca (Filtro JS)
                                        Significados = _context.VocabularyMeanings
                                                    .Where(m => m.VocabularyId == voca.Id && m.IdLanguage == idLinguaFinal)
                                                    .Select(m => m.Meaning)
                                                    .ToList()
                                    }).ToListAsync();

            // 4. Montar a ViewModel com Agrupamento
            var model = new VocabularyGeralViewModel
            {
                LinguaSelecionadaId = idLinguaFinal,
                CategoriaSelecionadaId = idCategoriaFinal,

                VocabPorNivel = vocabQuery
                    .GroupBy(v => string.IsNullOrEmpty(v.NivelCategoria) ? "Outros" : v.NivelCategoria)
                    // Tenta ordenar numericamente (para N5, N4, etc ou Níveis de 1 a 60 ficarem na ordem certa)
                    .OrderBy(g => int.TryParse(g.Key, out int num) ? num : 999)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => new VocabularyStatusDto
                        {
                            Id = x.Id,
                            Palavra = x.Palavra,
                            LeituraPrincipal = x.Leitura ?? "",
                            TemTraducao = x.TemTraducao,
                            SearchText = $"{x.Palavra} {x.Leitura} {string.Join(" ", x.Significados)}".ToLower()
                        }).ToList()
                    )
            };

            // 5. Preencher Dropdowns
            model.LinguasDisponiveis = await _context.Language.OrderBy(l => l.Description)
                .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Description, Selected = l.Id == idLinguaFinal })
                .ToListAsync();

            model.CategoriasDisponiveis = await _context.Categories.OrderBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name, Selected = c.Id == idCategoriaFinal })
                .ToListAsync();

            return View(model);
        }
    }