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

            // 3. QUERY COM SQL PURO 
            string sqlQuery = $@"
                SELECT 
                    v.id,
                    v.characters as Palavra,
                    vcm.category_level as NivelCategoria
                FROM vocabulary v
                INNER JOIN vocabulary_category_map vcm ON v.id = vcm.vocabulary_id
                WHERE vcm.category_id = {idCategoriaFinal}
            ";

            var dadosBase = new List<dynamic>();

            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sqlQuery;
                await _context.Database.OpenConnectionAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dadosBase.Add(new
                        {
                            Id = reader.GetInt32(0),
                            Palavra = reader.GetString(1),
                            NivelCategoria = reader.IsDBNull(2) ? "Outros" : reader.GetString(2)
                        });
                    }
                }
            }

            var idsVocabularios = dadosBase.Select(d => (int)d.Id).Distinct().ToList();

            if (!idsVocabularios.Any())
            {
                var modelVazio = new VocabularyGeralViewModel
                {
                    LinguaSelecionadaId = idLinguaFinal,
                    CategoriaSelecionadaId = idCategoriaFinal,
                    LinguasDisponiveis = await _context.Language.OrderBy(l => l.Description)
                        .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Description, Selected = l.Id == idLinguaFinal })
                        .ToListAsync(),
                    CategoriasDisponiveis = await _context.Categories.OrderBy(c => c.Name)
                        .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name, Selected = c.Id == idCategoriaFinal })
                        .ToListAsync()
                };
                return View(modelVazio);
            }

            var leituras = await _context.VocabularyReadings
                .Where(r => idsVocabularios.Contains(r.VocabularyId))
                .ToListAsync();

            var significados = await _context.VocabularyMeanings
                .Where(m => idsVocabularios.Contains(m.VocabularyId) && m.LanguageId == idLinguaFinal)
                .ToListAsync();

            var listaDtos = new List<VocabularyStatusDto>();

            foreach (var item in dadosBase)
            {
                int vId = item.Id;
                var leituraPrimaria = leituras
                    .Where(r => r.VocabularyId == vId)
                    .OrderByDescending(r => r.IsPrimary)
                    .Select(r => r.Reading)
                    .FirstOrDefault();

                var significadosDaPalavra = significados
                    .Where(m => m.VocabularyId == vId)
                    .Select(m => m.Meaning)
                    .ToList();

                listaDtos.Add(new VocabularyStatusDto
                {
                    Id = vId,
                    Palavra = item.Palavra,
                    LeituraPrincipal = leituraPrimaria ?? "",
                    TemTraducao = significadosDaPalavra.Any(),
                    SearchText = $"{item.Palavra} {leituraPrimaria} {string.Join(" ", significadosDaPalavra)}".ToLower(),
                    NivelCategoria = item.NivelCategoria
                });
            }

            var model = new VocabularyGeralViewModel
            {
                LinguaSelecionadaId = idLinguaFinal,
                CategoriaSelecionadaId = idCategoriaFinal,
                VocabPorNivel = listaDtos
                    .GroupBy(dto => dto.NivelCategoria)
                    .OrderByDescending(g => int.TryParse(g.Key, out int num) ? num : -1)
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToList()
                    )
            };

            model.LinguasDisponiveis = await _context.Language.OrderBy(l => l.Description)
                .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Description, Selected = l.Id == idLinguaFinal })
                .ToListAsync();

            model.CategoriasDisponiveis = await _context.Categories.OrderBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name, Selected = c.Id == idCategoriaFinal })
                .ToListAsync();

            return View(model);
        }

        public async Task<IActionResult> DetalhesModal(int? id)
        {
            if (id == null) return NotFound();

            var vocab = await _context.Vocabularies.FirstOrDefaultAsync(v => v.Id == id);
            if (vocab == null) return NotFound();

            var nivelCategoria = await _context.VocabularyCategoryMaps
                .Where(vcm => vcm.VocabularyId == id)
                .Select(vcm => vcm.CategoryLevel)
                .FirstOrDefaultAsync() ?? vocab.Level?.ToString() ?? "?";

            // 3. Busca as Leituras (Agora com o status do Mnemônico)
            var listaReadings = await (from r in _context.VocabularyReadings
                                       join rm in _context.Set<VocabularyReadingMnemonic>()
                                            on r.Id equals rm.VocabularyReadingId into rmGroup
                                       from rm in rmGroup.Where(x => x.IsActive == true).DefaultIfEmpty()
                                       where r.VocabularyId == id
                                       orderby r.IsPrimary descending
                                       select new VocabReadingDto
                                       {
                                           Id = r.Id,
                                           Reading = r.Reading,
                                           IsPrimary = r.IsPrimary ?? false,
                                           TemHistoria = rm != null
                                       }).ToListAsync();

            var classesGramaticais = await (from map in _context.Set<VocabularyPartOfSpeechMap>()
                                            join pos in _context.Set<VocabularyPartOfSpeech>() on map.VocabularyPartOfSpeechId equals pos.Id
                                            where map.VocabularyId == id
                                            select pos.Name).ToListAsync();

            var kanjisBase = await (from vc in _context.VocabularyCompositions
                                    join k in _context.Kanjis on vc.KanjiId equals k.Id
                                    where vc.VocabularyId == id
                                    select new { k.Id, k.Literal, k.JlptLevel }).ToListAsync();

            var kanjiIds = kanjisBase.Select(k => k.Id).ToList();
            var significadosKanji = await _context.Set<KanjiMeaning>()
                .Where(km => kanjiIds.Contains(km.KanjiId) && km.IdLanguageNavigation.LanguageCode == "pt-br")
                .ToListAsync();

            var listaKanjis = new List<KanjiComponenteDto>();
            foreach (var kb in kanjisBase)
            {
                var sig = significadosKanji.FirstOrDefault(s => s.KanjiId == kb.Id)?.Gloss ?? "Sem Tradução";
                listaKanjis.Add(new KanjiComponenteDto
                {
                    Id = kb.Id,
                    Caractere = kb.Literal,
                    Significado = sig,
                    Nivel = (int)(kb.JlptLevel ?? 0)
                });
            }

            var sentencas = new List<SentencaDto>();
            try
            {
                string sqlSentencas = $"SELECT id, ja, en FROM vocabulary_context_sentence WHERE vocabulary_id = {id}";
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = sqlSentencas;
                    if (command.Connection.State != System.Data.ConnectionState.Open)
                    {
                        await command.Connection.OpenAsync();
                    }
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            sentencas.Add(new SentencaDto
                            {
                                Id = reader.GetInt32(0),
                                Japones = reader.IsDBNull(1) ? "Sem texto" : reader.GetString(1),
                                Traducao = reader.IsDBNull(2) ? "Sem tradução" : reader.GetString(2)
                            });
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Erro ao carregar sentenças via SQL: {ex.Message}");
            }

            var significadosQuery = await (from m in _context.VocabularyMeanings
                                           join l in _context.Language on m.LanguageId equals l.Id
                                           join mn in _context.VocabularyMeaningMnemonics
                                                on m.Id equals mn.VocabularyMeaningId into mnGroup
                                           from mn in mnGroup.Where(x => x.IsActive == true).DefaultIfEmpty()
                                           where m.VocabularyId == id
                                           select new
                                           {
                                               Lingua = l.Description,
                                               SignificadoId = m.Id,
                                               Texto = m.Meaning,
                                               IsPrimary = m.IsPrimary,
                                               TemMnemonic = mn != null
                                           }).ToListAsync();

            var traducoesAgrupadas = significadosQuery
                .GroupBy(s => s.Lingua)
                .Select(g => new VocabGrupoTraducaoDto
                {
                    Lingua = g.Key,
                    Significados = g.Select(s => new VocabSignificadoDto
                    {
                        Id = s.SignificadoId,
                        Texto = s.Texto,
                        IsPrimary = s.IsPrimary,
                        TemHistoria = s.TemMnemonic
                    }).DistinctBy(s => s.Id).ToList()
                }).ToList();

            var linguas = await _context.Language.OrderBy(l => l.Description)
                .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Description })
                .ToListAsync();

            var linguaPtBr = linguas.FirstOrDefault(l => l.Text.Contains("Português") || l.Text.Contains("Portuguese"));
            int linguaPadraoId = linguaPtBr != null ? int.Parse(linguaPtBr.Value) : (linguas.Any() ? int.Parse(linguas.First().Value) : 1);

            var listaSyntax = await _context.Set<SyntaxHighlight>().ToListAsync();

            var model = new VocabularyDetalhesViewModel
            {
                Id = vocab.Id,
                Palavra = vocab.Characters,
                Nivel = nivelCategoria,
                ListaReadings = listaReadings,
                ClassesGramaticais = classesGramaticais,
                KanjisComponentes = listaKanjis,
                Sentencas = sentencas,
                TraducoesAgrupadas = traducoesAgrupadas,
                LinguasDisponiveis = linguas,
                LinguaSelecionadaId = linguaPadraoId,
                ListaSyntax = listaSyntax
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
                Meaning = meaning
            };

            _context.VocabularyMeanings.Add(newMeaning);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // =======================================================
        // GESTÃO DE MNEMÔNICOS DO VOCABULÁRIO
        // =======================================================

        [HttpGet]
        public async Task<IActionResult> GetMeaningMnemonic(int meaningId)
        {
            var mnemonic = await _context.VocabularyMeaningMnemonics
                .FirstOrDefaultAsync(m => m.VocabularyMeaningId == meaningId && m.IsActive == true);

            string rawText = mnemonic?.Text ?? "";
            string formattedText = rawText;

            if (!string.IsNullOrEmpty(rawText))
            {
                var regras = await _context.Set<SyntaxHighlight>().ToListAsync();
                foreach (var regra in regras)
                {
                    formattedText = formattedText.Replace($"<{regra.Code}>", $"<span style='color:{regra.TextColor}; font-weight:bold;'>");
                    formattedText = formattedText.Replace($"</{regra.Code}>", "</span>");
                }
                formattedText = formattedText.Replace("\n", "<br>");
            }

            return Json(new { id = mnemonic?.Id ?? 0, text = rawText, formattedText = formattedText });
        }

        [HttpPost]
        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> SaveMeaningMnemonic(int meaningId, string text, int mnemonicId)
        {
            // 1. Descobre a qual vocabulário e a qual idioma esta tradução pertence
            var meaning = await _context.VocabularyMeanings.FindAsync(meaningId);

            if (meaning != null)
            {
                int vocabId = meaning.VocabularyId;

                // Pega o ID da língua (Se a sua propriedade puder ser nula, usamos o ?? 0)
                int langId = meaning.LanguageId ;

                // 2. Tira o 'Primary' de TODAS as traduções desta palavra APENAS NESTE IDIOMA
                // Obs: Usei "language_id", mas se no seu banco a coluna for exatamente "id_language", basta trocar ali no SQL!
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE vocabulary_meaning SET is_primary = false WHERE vocabulary_id = {0} AND id_language = {1}", vocabId, langId);

                // 3. Coloca o 'Primary' APENAS nesta tradução que acabamos de salvar a história
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE vocabulary_meaning SET is_primary = true WHERE id = {0}", meaningId);
            }

            // 4. Salva o Mnemônico
            if (mnemonicId > 0)
            {
                var m = await _context.VocabularyMeaningMnemonics.FindAsync(mnemonicId);
                if (m != null)
                {
                    m.Text = text;
                    m.UpdatedAt = System.DateTime.UtcNow;
                }
            }
            else
            {
                _context.VocabularyMeaningMnemonics.Add(new VocabularyMeaningMnemonic
                {
                    VocabularyMeaningId = meaningId,
                    Text = text
                });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
        [HttpPost]
        public async Task<IActionResult> DeleteMeaningMnemonic(int mnemonicId)
        {
            var m = await _context.VocabularyMeaningMnemonics.FindAsync(mnemonicId);
            if (m != null)
            {
                m.IsActive = false;
                m.UpdatedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }
    
        // COLOQUE OS NOVOS MÉTODOS AQUI, LOGO ABAIXO DO DELETE MEANING!

        // =======================================================
        // GESTÃO DE MNEMÔNICOS DE LEITURA (READING)
        // =======================================================

        [HttpGet]
        public async Task<IActionResult> GetReadingMnemonic(int readingId)
        {
            var mnemonic = await _context.VocabularyReadingMnemonics
                .FirstOrDefaultAsync(m => m.VocabularyReadingId == readingId && m.IsActive == true);

            string rawText = mnemonic?.Text ?? "";
            string formattedText = rawText;

            if (!string.IsNullOrEmpty(rawText))
            {
                var regras = await _context.Set<SyntaxHighlight>().ToListAsync();
                foreach (var regra in regras)
                {
                    formattedText = formattedText.Replace($"<{regra.Code}>", $"<span style='color:{regra.TextColor}; font-weight:bold;'>");
                    formattedText = formattedText.Replace($"</{regra.Code}>", "</span>");
                }
                formattedText = formattedText.Replace("\n", "<br>");
            }

            return Json(new { id = mnemonic?.Id ?? 0, texto = rawText, formattedText = formattedText });
        }

        [HttpPost]
        public async Task<IActionResult> SaveReadingMnemonic(int readingId, string texto)
        {
            var reading = await _context.VocabularyReadings.FindAsync(readingId);
            if (reading != null)
            {
                int vocabId = reading.VocabularyId;

                // Tira o 'Primary' das outras leituras desta palavra e define na atual
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE vocabulary_reading SET is_primary = false WHERE vocabulary_id = {0}", vocabId);
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE vocabulary_reading SET is_primary = true WHERE id = {0}", readingId);
            }

            var mnemonic = await _context.VocabularyReadingMnemonics
                .FirstOrDefaultAsync(x => x.VocabularyReadingId == readingId && x.IsActive == true);

            if (mnemonic != null)
            {
                mnemonic.Text = texto;
                mnemonic.UpdatedAt = System.DateTime.UtcNow;
            }
            else
            {
                _context.VocabularyReadingMnemonics.Add(new VocabularyReadingMnemonic
                {
                    VocabularyReadingId = readingId,
                    Text = texto
                });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> ExcluirHistoriaReading(int readingId)
        {
            var mnemonic = await _context.VocabularyReadingMnemonics
                .FirstOrDefaultAsync(x => x.VocabularyReadingId == readingId && x.IsActive == true);

            if (mnemonic != null)
            {
                mnemonic.IsActive = false;
                mnemonic.UpdatedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }
    }
}