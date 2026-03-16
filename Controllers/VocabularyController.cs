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

        public async Task<IActionResult> Index(int? linguaId, int? categoriaId, int? circuloId, bool switchModalVocabulario = false)
        {

            // 1. Configurar Língua Padrão (Português)
            var linguaPadrao = await _context.Language.FirstOrDefaultAsync(l => l.LanguageCode == "pt-br")
                            ?? await _context.Language.FirstOrDefaultAsync();
            int idLinguaFinal = linguaId ?? linguaPadrao?.Id ?? 1;


            // 2. Configurar Categoria Padrão (Ex: JLPT)
            var categoriaPadrao = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "JLPT")
                               ?? await _context.Categories.FirstOrDefaultAsync();
            int idCategoriaFinal = categoriaId ?? categoriaPadrao?.Id ?? 1;

            // =======================================================
            // NOVA LÓGICA: MONTAR LISTA DE CATEGORIAS AQUI
            // =======================================================
            var categoriasDisponiveis = await _context.Categories.OrderBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name, Selected = c.Id == idCategoriaFinal })
                .ToListAsync();

            // Adiciona a Categoria Virtual "JLPT - Wanikani" com ID 999
            categoriasDisponiveis.Add(new SelectListItem
            {
                Value = "999",
                Text = "JLPT - Wanikani",
                Selected = idCategoriaFinal == 999
            });
            // =======================================================

            // =======================================================
            // CARREGAR CÍRCULOS PARA O DROPDOWN
            // =======================================================
            var circulosDisponiveis = new List<SelectListItem> { new SelectListItem { Value = "0", Text = "-- Todos os Círculos --" } };
            int idCirculoFinal = circuloId ?? 0;

            circulosDisponiveis.Add(new SelectListItem
            {
                Value = "500",
                Text = "-- Sem kanji --",
                Selected = idCirculoFinal == 500  
            });

            using (var cmd = _context.Database.GetDbConnection().CreateCommand())
            {
                if (cmd.Connection.State != System.Data.ConnectionState.Open) await cmd.Connection.OpenAsync();
                cmd.CommandText = @"
                    SELECT c.id, m.sequential, m.text, c.sequential, c.text
                    FROM circle c JOIN mandala m ON c.mandala_id = m.id
                    ORDER BY m.sequential, c.sequential";

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        circulosDisponiveis.Add(new SelectListItem
                        {
                            Value = reader.GetInt32(0).ToString(),
                            Text = $"M{reader.GetInt32(1)}.C{(reader.IsDBNull(3) ? 0 : reader.GetInt32(3))} - {reader.GetString(4)} ({reader.GetString(2)})",
                            Selected = reader.GetInt32(0) == idCirculoFinal
                        });
                    }
                }
            }

            // =======================================================
            // MONTAR A "MÁGICA" DO FILTRO EM CASCATA
            // =======================================================

            string sqlFiltroCirculo = "";

            if (idCirculoFinal == 500)
            {
                // Opção: Sem Kanji
                sqlFiltroCirculo = @"
                    AND v.id NOT IN (SELECT vocabulary_id FROM vocabulary_composition)";
            }
            else if (idCirculoFinal > 0)
            {
                if (switchModalVocabulario)
                {
                    // MODO RESTRITO (SWITCH LIGADO): Traz apenas vocabulários associados diretamente a este círculo
                    sqlFiltroCirculo = $@"
                        AND v.id IN (SELECT vocabulary_id FROM circle_ue_item WHERE circle_id = {idCirculoFinal} AND vocabulary_id IS NOT NULL)";
                }
                else
                {
                    // MODO EXPANDIDO (SWITCH DESLIGADO): Traz do círculo OU os que contêm kanjis do círculo
                    sqlFiltroCirculo = $@"
                        AND (
                            -- 1. O Vocabulário está neste Círculo
                            v.id IN (SELECT vocabulary_id FROM circle_ue_item WHERE circle_id = {idCirculoFinal} AND vocabulary_id IS NOT NULL)
                            
                            -- 2. OU O Vocabulário tem um Kanji que está neste Círculo
                            OR v.id IN (
                                SELECT vc.vocabulary_id FROM vocabulary_composition vc 
                                WHERE vc.kanji_id IN (SELECT kanji_id FROM circle_ue_item WHERE circle_id = {idCirculoFinal} AND kanji_id IS NOT NULL)
                            )
                        )";
                }
            }

            // 3. QUERY COM SQL PURO 
            string sqlQuery = "";

            if (idCategoriaFinal == 999)
            {
                // A SUA QUERY PERSONALIZADA (JLPT - Wanikani)
                sqlQuery = $@"
                                   SELECT DISTINCT
                                       v.id,
                                       v.characters as Palavra,
                                       'JLPT ' || vcm.category_level as NivelCategoria,
                                       v.is_active
                                   FROM vocabulary v
	                                INNER JOIN vocabulary_category_map vcm ON v.id = vcm.vocabulary_id
                                   WHERE v.wanikani_id IS NULL
	                               --and vcm.category_id = {idCategoriaFinal}
                    {sqlFiltroCirculo}  /* INJETA O FILTRO DE CÍRCULOS AQUI TBM */

                                union all
                                   SELECT 
                                       v.id,
                                       v.characters as Palavra,
                                         'Wanikani sem JLPT '|| vcm.category_level as NivelCategoria,
                                       v.is_active
                                   FROM vocabulary v
	                                INNER JOIN vocabulary_category_map vcm ON v.id = vcm.vocabulary_id
                                   WHERE v.wanikani_id IS not NULL  
	                               --and vcm.category_id = {idCategoriaFinal}
                                   AND v.characters NOT IN (
                                       SELECT characters FROM vocabulary WHERE wanikani_id IS  NULL
                    )
                    {sqlFiltroCirculo}  /* INJETA O FILTRO DE CÍRCULOS AQUI TBM */
                ";
            }
            else
            {
                // A QUERY ORIGINAL (Sistemas Normais)
                sqlQuery = $@"
                    SELECT DISTINCT
                        v.id,
                        v.characters as Palavra,
                        vcm.category_level as NivelCategoria,
                        v.is_active
                    FROM vocabulary v
                    INNER JOIN vocabulary_category_map vcm ON v.id = vcm.vocabulary_id
                    WHERE vcm.category_id = {idCategoriaFinal}
                    {sqlFiltroCirculo}  /* INJETA A MÁGICA AQUI */
                ";
            }

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
                            NivelCategoria = reader.IsDBNull(2) ? "Outros" : reader.GetString(2),
                            // LÊ O STATUS DO BANCO AQUI (coluna índice 3):
                            IsActive = reader.GetBoolean(3)
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
                    CirculoSelecionadoId = idCirculoFinal,
                    CirculosDisponiveis = circulosDisponiveis,
                    SwitchModalVocabulario = switchModalVocabulario,
                    LinguasDisponiveis = await _context.Language.OrderBy(l => l.Description)
                        .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Description, Selected = l.Id == idLinguaFinal })
                        .ToListAsync(),
                    CategoriasDisponiveis = categoriasDisponiveis

                };
                modelVazio.CategoriasDisponiveis = categoriasDisponiveis;
                modelVazio.CirculosDisponiveis = circulosDisponiveis; // Adicione também se não estiver!
                return View(modelVazio);
            }

            var leituras = await _context.VocabularyReadings
                .Where(r => idsVocabularios.Contains(r.VocabularyId))
                .ToListAsync();

            var significados = await _context.VocabularyMeanings
                .Where(m => idsVocabularios.Contains(m.VocabularyId) && m.LanguageId == idLinguaFinal)
                .ToListAsync();

            // ==========================================
            // NOVO: BUSCA OS MNEMÔNICOS EXISTENTES
            // ==========================================
            var idsSignificados = significados.Select(s => s.Id).ToList();
            var idsLeituras = leituras.Select(l => l.Id).ToList();

            var mnemonicosSignificado = await _context.VocabularyMeaningMnemonics
                .Where(m => idsSignificados.Contains(m.VocabularyMeaningId) && m.IsActive == true)
                .Select(m => m.VocabularyMeaningId)
                .Distinct()
                .ToListAsync();

            var mnemonicosLeitura = await _context.VocabularyReadingMnemonics
                .Where(m => idsLeituras.Contains(m.VocabularyReadingId) && m.IsActive == true)
                .Select(m => m.VocabularyReadingId)
                .Distinct()
                .ToListAsync();
            // ==========================================

            // ==========================================
            // NOVO: BUSCA SE OS ITENS ESTÃO NO ARQUITETO (CÍRCULOS)
            // ==========================================
            var vocabIdsNoArquiteto = new List<int>();
            if (idsVocabularios.Any())
            {
                string idsStr = string.Join(",", idsVocabularios);
                string sqlCircle = $"SELECT DISTINCT vocabulary_id FROM circle_ue_item WHERE vocabulary_id IN ({idsStr})";

                using (var commandCircle = _context.Database.GetDbConnection().CreateCommand())
                {
                    commandCircle.CommandText = sqlCircle;
                    if (commandCircle.Connection.State != System.Data.ConnectionState.Open)
                    {
                        await commandCircle.Connection.OpenAsync();
                    }
                    using (var readerCircle = await commandCircle.ExecuteReaderAsync())
                    {
                        while (await readerCircle.ReadAsync())
                        {
                            vocabIdsNoArquiteto.Add(Convert.ToInt32(readerCircle[0]));
                        }
                    }
                }
            }
            // ==========================================

            var listaDtos = new List<VocabularyStatusDto>();

            foreach (var item in dadosBase)
            {
                int vId = item.Id;

                // Filtra o que pertence a esta palavra específica
                var leitsPalavra = leituras.Where(r => r.VocabularyId == vId).ToList();
                var sigsPalavra = significados.Where(m => m.VocabularyId == vId).ToList();

                var leituraPrimaria = leituras
                    .Where(r => r.VocabularyId == vId)
                    .OrderByDescending(r => r.IsPrimary)
                    .Select(r => r.Reading)
                    .FirstOrDefault();

                var significadosDaPalavra = significados
                    .Where(m => m.VocabularyId == vId)
                    .Select(m => m.Meaning)
                    .ToList();

                // ==========================================
                // VERIFICA SE ESTÁ 100% COMPLETO E NO ARQUITETO
                // ==========================================
                bool temSignificado = sigsPalavra.Any();
                bool temLeitura = leitsPalavra.Any();
                bool temMnemSig = sigsPalavra.Any(s => mnemonicosSignificado.Contains(s.Id));
                bool temMnemLei = leitsPalavra.Any(l => mnemonicosLeitura.Contains(l.Id));

                bool is100PorCento = temSignificado && temLeitura && temMnemSig && temMnemLei;
                bool estaNoArquiteto = vocabIdsNoArquiteto.Contains(vId); // NOVA FLAG AQUI
                // ==========================================

                listaDtos.Add(new VocabularyStatusDto
                {
                    Id = vId,
                    Palavra = item.Palavra,
                    LeituraPrincipal = leituraPrimaria ?? "",
                    TemTraducao = temSignificado,
                    IsCompleto = is100PorCento,
                    AssociadoCirculo = estaNoArquiteto, // ATRIBUI A NOVA FLAG
                    SearchText = $"{item.Palavra} {leituraPrimaria} {string.Join(" ", significadosDaPalavra)}".ToLower(),
                    NivelCategoria = item.NivelCategoria,
                    IsActive = item.IsActive
                });
            }

            var model = new VocabularyGeralViewModel
            {
                LinguaSelecionadaId = idLinguaFinal,
                CategoriaSelecionadaId = idCategoriaFinal,
                CirculoSelecionadoId = idCirculoFinal,
                CirculosDisponiveis = circulosDisponiveis,
                VocabPorNivel = listaDtos
                      .GroupBy(dto => dto.NivelCategoria)
                      .OrderByDescending(g => int.TryParse(g.Key, out int num) ? num : -1)
                      .ToDictionary(
                          g => g.Key,
                          g => g.OrderByDescending(v => v.IsActive)    // 1º: Ativos no topo, Cancelados no fim
                                .ThenByDescending(v => v.IsCompleto)   // 2º: Dentre os ativos, os 100% Completos vêm primeiro
                                .ThenBy(v => v.Palavra)                // 3º: Desempata por ordem alfabética (opcional, mas fica lindo!)
                                .ToList()
                      )
            };

            model.LinguasDisponiveis = await _context.Language.OrderBy(l => l.Description)
                .Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Description, Selected = l.Id == idLinguaFinal })
                .ToListAsync();

            model.CategoriasDisponiveis = categoriasDisponiveis;

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
                                            select new VocabPartOfSpeechDto { PosId = pos.Id, Nome = pos.Name }).ToListAsync();

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

            var posUsadosIds = classesGramaticais.Select(c => c.PosId).ToList();
            var classesDisponiveis = await _context.Set<VocabularyPartOfSpeech>()
                .Where(p => p.language_id == linguaPadraoId && !posUsadosIds.Contains(p.Id))
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                .OrderBy(p => p.Text)
                .ToListAsync();

            var listaSyntax = await _context.Set<SyntaxHighlight>().ToListAsync();

            // =================================================================
            // BUSCA DOS CÍRCULOS (ARQUITETO) PARA O DROPDOWN
            // =================================================================
            var circulosDisponiveis = new List<SelectListItem> { new SelectListItem { Value = "0", Text = "-- Não Associado --" } };
            int? currentCircleId = null;

            using (var cmd = _context.Database.GetDbConnection().CreateCommand())
            {
                if (cmd.Connection.State != System.Data.ConnectionState.Open) await cmd.Connection.OpenAsync();

                // 1. Busca todos os círculos formatados (Ex: M1.C2 - Água)
                cmd.CommandText = @"
                    SELECT c.id, m.sequential, m.text, c.sequential, c.text
                    FROM circle c
                    JOIN mandala m ON c.mandala_id = m.id
                    ORDER BY m.sequential, c.sequential";

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int cId = reader.GetInt32(0);
                        int mSeq = reader.GetInt32(1);
                        string mText = reader.GetString(2);
                        int cSeq = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        string cText = reader.GetString(4);

                        circulosDisponiveis.Add(new SelectListItem
                        {
                            Value = cId.ToString(),
                            Text = $"M{mSeq}.C{cSeq} - {cText} ({mText})"
                        });
                    }
                }

                // 2. Busca o círculo atual deste vocabulário (se existir)
                cmd.CommandText = $"SELECT circle_id FROM circle_ue_item WHERE vocabulary_id = {id} LIMIT 1";
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value) currentCircleId = Convert.ToInt32(result);
            }
            // =================================================================

            var model = new VocabularyDetalhesViewModel
            {
                Id = vocab.Id,
                Palavra = vocab.Characters,
                Nivel = nivelCategoria,
                IsActive = vocab.IsActive,
                CirculoAtualId = currentCircleId,
                CirculosDisponiveis = circulosDisponiveis,
                ListaReadings = listaReadings,
                ClassesGramaticais = classesGramaticais,
                ClassesGramaticaisDisponiveis = classesDisponiveis,
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
                IsActive = vocabulary.IsActive,
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
        [HttpPost]
        public async Task<IActionResult> ExcluirTraducao(int meaningId)
        {
            var meaning = await _context.VocabularyMeanings.FindAsync(meaningId);
            if (meaning != null)
            {
                // 1. Busca as histórias atreladas
                var mnemonicos = await _context.VocabularyMeaningMnemonics
                    .Where(m => m.VocabularyMeaningId == meaningId)
                    .ToListAsync();

                // Salva a exclusão das histórias PRIMEIRO (Evita o conflito de concorrência)
                if (mnemonicos.Any())
                {
                    _context.VocabularyMeaningMnemonics.RemoveRange(mnemonicos);
                    await _context.SaveChangesAsync();
                }

                // 2. Remove a tradução e salva novamente
                _context.VocabularyMeanings.Remove(meaning);
                await _context.SaveChangesAsync();
            }
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
        public async Task<IActionResult> SaveMeaningMnemonic(int meaningId, string text, int mnemonicId)
        {
            // 1. Descobre a qual vocabulário e a qual idioma esta tradução pertence
            var meaning = await _context.VocabularyMeanings.FindAsync(meaningId);

            if (meaning != null)
            {
                int vocabId = meaning.VocabularyId;

                // Pega o ID da língua (Se a sua propriedade puder ser nula, usamos o ?? 0)
                int langId = meaning.LanguageId;

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
        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id, bool isActive)
        {
            var vocab = await _context.Vocabularies.FindAsync(id);
            if (vocab == null) return NotFound();

            vocab.IsActive = isActive;

            // A MÁGICA AQUI: Se estiver a desativar, corta o vínculo com o Arquiteto!
            if (!isActive)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM circle_ue_item WHERE vocabulary_id = {0}", id);
            }

            await _context.SaveChangesAsync();

            return Ok();
        }
        [HttpPost]
        public async Task<IActionResult> AssociarCirculo(int vocabId, int circleId)
        {
            using (var cmd = _context.Database.GetDbConnection().CreateCommand())
            {
                if (cmd.Connection.State != System.Data.ConnectionState.Open) await cmd.Connection.OpenAsync();

                if (circleId == 0)
                {
                    // Remove do arquiteto se escolher "Não Associado"
                    cmd.CommandText = $"DELETE FROM circle_ue_item WHERE vocabulary_id = {vocabId}";
                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    // Verifica se já tem registro
                    cmd.CommandText = $"SELECT id FROM circle_ue_item WHERE vocabulary_id = {vocabId} LIMIT 1";
                    var idResult = await cmd.ExecuteScalarAsync();

                    if (idResult != null && idResult != DBNull.Value)
                    {
                        // Atualiza para o novo círculo
                        cmd.CommandText = $"UPDATE circle_ue_item SET circle_id = {circleId} WHERE vocabulary_id = {vocabId}";
                        await cmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // Insere como o último item do círculo escolhido
                        cmd.CommandText = $"SELECT COALESCE(MAX(sequential), 0) + 1 FROM circle_ue_item WHERE circle_id = {circleId}";
                        var seqResult = await cmd.ExecuteScalarAsync();
                        int nextSeq = seqResult != DBNull.Value ? Convert.ToInt32(seqResult) : 1;

                        cmd.CommandText = $"INSERT INTO circle_ue_item (circle_id, vocabulary_id, sequential) VALUES ({circleId}, {vocabId}, {nextSeq})";
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            return Ok();
        }
        [HttpPost]
        public async Task<IActionResult> RemoverClasseGramatical(int vocabId, int posId)
        {
            var map = await _context.Set<VocabularyPartOfSpeechMap>()
                .FirstOrDefaultAsync(m => m.VocabularyId == vocabId && m.VocabularyPartOfSpeechId == posId);

            if (map != null)
            {
                _context.Set<VocabularyPartOfSpeechMap>().Remove(map);
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AdicionarClasseGramatical(int vocabId, int posId)
        {
            var existe = await _context.Set<VocabularyPartOfSpeechMap>()
                .AnyAsync(m => m.VocabularyId == vocabId && m.VocabularyPartOfSpeechId == posId);

            if (!existe)
            {
                _context.Set<VocabularyPartOfSpeechMap>().Add(new VocabularyPartOfSpeechMap
                {
                    VocabularyId = vocabId,
                    VocabularyPartOfSpeechId = posId
                });
                await _context.SaveChangesAsync();
            }
            return Ok();
        }
    }
}