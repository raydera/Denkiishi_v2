using Denkiishi_v2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        // 1. INDEX
        // ==========================================================
        /// <summary>
        /// Renderiza o mapa de Kanjis com busca e filtros (Categoria/Língua/Círculo).
        ///
        /// Regra de negócio (subconjunto de radicais): quando <paramref name="circleId"/> é informado,
        /// calculamos o conjunto de radicais conhecidos do aluno dentro da mesma Mandala (todos os círculos
        /// com <c>sequential</c> menor ou igual ao círculo atual). Em seguida, selecionamos Kanjis cujo conjunto
        /// de radicais em <c>kanji_radical</c> é um subconjunto integral desse conjunto conhecido.
        ///
        /// Essa é a mesma ideia do validador do Arquiteto (<c>ValidateMatrix</c>): um item só “faz sentido”
        /// quando seus requisitos (radicais) já foram introduzidos em círculos anteriores da mesma Mandala.
        /// </summary>
        public async Task<IActionResult> Index(int? categoriaId, int? linguaId, int? circleId, bool switchModalKanji = false)
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

            // B. Carregar Línguas
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

            // D. Carregar Círculos (Arquiteto) para o dropdown
            viewModel.CirculoId = circleId;
            viewModel.SwitchModalKanji = switchModalKanji;

            var circulosItens = new List<SelectListItem>();
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
                        int cId = reader.GetInt32(0);
                        int mSeq = reader.GetInt32(1);
                        string mText = reader.GetString(2);
                        int cSeq = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        string cText = reader.GetString(4);

                        circulosItens.Add(new SelectListItem
                        {
                            Value = cId.ToString(),
                            Text = $"M{mSeq}.C{cSeq} - {cText} ({mText})",
                            Selected = (circleId.HasValue && cId == circleId.Value)
                        });
                    }
                }
            }

            viewModel.CirculosDisponiveis = new SelectList(circulosItens, "Value", "Text", circleId?.ToString());

            // C. Buscar Kanjis
            if (viewModel.CategoriaSelecionadaId > 0)
            {
                var query = _context.KanjiCategoryMaps
                    .Include(kc => kc.Kanji)
                        .ThenInclude(k => k.KanjiMeanings)
                    .Where(kc => kc.CategoryId == viewModel.CategoriaSelecionadaId)
                    .Where(kc => kc.Kanji.IsActive == true);

                // Filtro por Círculo (Arquiteto)
                if (circleId.HasValue)
                {
                    int cId = circleId.Value;

                    // ARCH: RadialValidation
                    // Identifica Mandala/Sequencial do círculo atual para aplicar a regra de subconjunto.
                    var circleInfo = await _context.Circles
                        .AsNoTracking()
                        .Where(c => c.Id == cId)
                        .Select(c => new { c.Id, c.MandalaId, c.Sequential })
                        .FirstOrDefaultAsync();

                    // Se o circleId for inválido, simplesmente não aplica filtro de círculo (fail-safe).
                    if (circleInfo != null)
                    {
                        // ARCH: RadialValidation
                        // Conjunto de radicais conhecidos: todos radicais mapeados em círculos <= sequencial atual (na mesma mandala).
                        var knownRadicalIds = await (
                            from cui in _context.CircleUeItems.AsNoTracking()
                            join c in _context.Circles.AsNoTracking() on cui.CircleId equals c.Id
                            where c.MandalaId == circleInfo.MandalaId
                                  && c.Sequential <= circleInfo.Sequential
                                  && cui.RadicalId != null
                            select cui.RadicalId!.Value
                        ).Distinct().ToListAsync();

                        // Conjunto de kanjis nativos do círculo atual.
                        var nativeKanjiIdsQuery = _context.CircleUeItems
                            .AsNoTracking()
                            .Where(x => x.CircleId == cId && x.KanjiId != null)
                            .Select(x => x.KanjiId!.Value);

                        // ARCH: RadialValidation
                        // Kanjis “válidos pela lógica”: todos seus radicais estão contidos em knownRadicalIds (divisão relacional).
                        // Implementação por contagem para tradução SQL eficiente.
                        var suggestedByLogicKanjiIdsQuery = _context.KanjiRadicals
                            .AsNoTracking()
                            .GroupBy(kr => kr.KanjiId)
                            .Where(g =>
                                g.Count() == g.Count(kr => knownRadicalIds.Contains(kr.RadicalId))
                            )
                            .Select(g => g.Key);

                        // Filtro final do mapa: nativos OU sugeridos pela lógica (independente do modo “Estrito”).
                        // O switchModalKanji mantém apenas o “expandido” de vocabulários para o conjunto nativo (legado).
                        if (switchModalKanji)
                        {
                            query = query.Where(kc =>
                                nativeKanjiIdsQuery.Contains(kc.KanjiId) ||
                                suggestedByLogicKanjiIdsQuery.Contains(kc.KanjiId)
                            );
                        }
                        else
                        {
                            var vocabIdsNoCirculo = _context.CircleUeItems
                                .AsNoTracking()
                                .Where(x => x.CircleId == cId && x.VocabularyId != null)
                                .Select(x => x.VocabularyId!.Value);

                            var kanjiIdsDeVocabsNoCirculo = _context.VocabularyCompositions
                                .AsNoTracking()
                                .Where(vc => vocabIdsNoCirculo.Contains(vc.VocabularyId))
                                .Select(vc => vc.KanjiId);

                            query = query.Where(kc =>
                                nativeKanjiIdsQuery.Contains(kc.KanjiId) ||
                                kanjiIdsDeVocabsNoCirculo.Contains(kc.KanjiId) ||
                                suggestedByLogicKanjiIdsQuery.Contains(kc.KanjiId)
                            );
                        }

                        // Materializa conjuntos para flags no DTO (evita N+1 / Contains em loop no client).
                        var nativeKanjiIds = await nativeKanjiIdsQuery.Distinct().ToListAsync();
                        var suggestedByLogicKanjiIds = await suggestedByLogicKanjiIdsQuery.Distinct().ToListAsync();

                        // Executa a query já filtrada e projeta DTOs com flags.
                        var dadosFiltrados = await query.ToListAsync();

                        viewModel.KanjisPorNivel = dadosFiltrados
                            .GroupBy(kc => kc.CategoryLevel)
                            .OrderBy(g => g.Key)
                            .ToDictionary(
                                g => g.Key ?? "Outros",
                                g => g.Select(m => new KanjiStatusDto
                                {
                                    Id = m.Kanji.Id,
                                    Literal = m.Kanji.Literal,
                                    TemTraducao = m.Kanji.KanjiMeanings.Any(km => km.IdLanguage == viewModel.LinguaSelecionadaId),
                                    SearchText = (m.Kanji.Literal + " " + string.Join(" ", m.Kanji.KanjiMeanings.Select(km => km.Gloss))).ToLower(),
                                    IsNativeToCircle = nativeKanjiIds.Contains(m.Kanji.Id),
                                    IsSuggestedByLogic = suggestedByLogicKanjiIds.Contains(m.Kanji.Id) && !nativeKanjiIds.Contains(m.Kanji.Id)
                                }).ToList()
                            );

                        return View(viewModel);
                    }

                }

                var dados = await query.ToListAsync();

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
                            SearchText = (m.Kanji.Literal + " " + string.Join(" ", m.Kanji.KanjiMeanings.Select(km => km.Gloss))).ToLower(),
                            IsNativeToCircle = false,
                            IsSuggestedByLogic = false
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
            var model = await MontarViewModelDetalhes(id);
            if (model == null) return NotFound();

            // Retorna a PartialView que contém o Modal Deslizante
            // Certifique-se que o nome do arquivo cshtml está correto (ex: "_ModalContent" ou "DetalhesModal")
            return PartialView("_ModalContent", model);
        }

        // ==========================================================
        // 3. DETALHES (Para Tela Cheia - Retorna View Completa)
        // ==========================================================
        public async Task<IActionResult> Detalhes(int? id)
        {
            var model = await MontarViewModelDetalhes(id);
            if (model == null) return NotFound();

            return View("Detalhes", model);
        }

   
        // ==========================================================
        // 4. SALVAR HISTÓRIA (ENDPOINT REAL)
        // ==========================================================
        [HttpPost]
        public async Task<IActionResult> SalvarHistoriaMeaning([FromBody] SalvarHistoriaRequest request)
        {
            if (request == null || request.MeaningId <= 0)
                return BadRequest("Dados inválidos.");

            if (string.IsNullOrWhiteSpace(request.Texto))
                return BadRequest("O texto da história não pode estar vazio.");

            try
            {
                // 1. Versionamento: Desativar histórias antigas
                var historiasAntigas = await _context.KanjiMeaningMnemonics
                    .Where(m => m.KanjiMeaningId == request.MeaningId && m.IsActive)
                    .ToListAsync();

                if (historiasAntigas.Any())
                {
                    foreach (var h in historiasAntigas)
                    {
                        h.IsActive = false;
                        h.UpdatedAt = DateTime.UtcNow; // FORÇA UTC
                    }
                }

                // 2. Criar nova história
                var novaHistoria = new KanjiMeaningMnemonic
                {
                    KanjiMeaningId = request.MeaningId,
                    Text = request.Texto,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow, // FORÇA UTC
                    UpdatedAt = DateTime.UtcNow  // FORÇA UTC
                };

                _context.KanjiMeaningMnemonics.Add(novaHistoria);

                // 3. Commit no Banco
                await _context.SaveChangesAsync();

                return Ok(new { mensagem = "História salva com sucesso!" });
            }
            catch (DbUpdateException dbEx)
            {
                // Captura erros específicos de banco de dados e pega a mensagem interna
                var erroReal = dbEx.InnerException != null ? dbEx.InnerException.Message : dbEx.Message;
                return StatusCode(500, $"Erro de Banco: {erroReal}");
            }
            catch (Exception ex)
            {
                // Captura outros erros genéricos
                var erroReal = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Erro Interno: {erroReal}");
            }
        }

        // Classe DTO para receber o JSON do JavaScript
        public class SalvarHistoriaRequest
        {
            public int MeaningId { get; set; }
            public string Texto { get; set; }
        }
        // ==========================================================
        // MÉTODO AUXILIAR (Monta os dados e verifica se tem história)
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

            // Buscar IDs de Significados que têm história ativa (para performance, busca todos de uma vez)
            var idsMeaningsDoKanji = kanji.KanjiMeanings.Select(m => m.Id).ToList();
            var meaningsComHistoria = await _context.KanjiMeaningMnemonics
                .Where(m => idsMeaningsDoKanji.Contains(m.KanjiMeaningId) && m.IsActive)
                .Select(m => m.KanjiMeaningId)
                .ToListAsync();

            // Buscar IDs de Readings que têm história ativa
            var idsReadingsDoKanji = kanji.KanjiReadings.Select(r => r.Id).ToList();
            var readingsComHistoria = await _context.KanjiReadingMnemonics
                .Where(rm => idsReadingsDoKanji.Contains(rm.KanjiReadingId) && rm.IsActive)
                .Select(rm => rm.KanjiReadingId)
                .ToListAsync();

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

                // --- NOVO: Carregar a lista de estilos para a Toolbar ---
                ListaSyntax = await _context.SyntaxHighlights
                                    .AsNoTracking() // Performance: apenas leitura
                                    .OrderBy(x => x.Code)
                                    .ToListAsync(),

                // AQUI ESTÁ A LÓGICA CORRIGIDA E INTEGRADA
                TraducoesAgrupadas = kanji.KanjiMeanings
                    .Where(m => m.IdLanguageNavigation != null)
                    .GroupBy(m => m.IdLanguageNavigation.Description)
                    .Select(g => new GrupoTraducaoDto
                    {
                        Lingua = g.Key ?? "Indefinida",
                        Significados = g.Select(m => new SignificadoDto
                        {
                            Id = m.Id,
                            Texto = m.Gloss,
                            IsPrincipal = m.IsPrincipal ?? false,
                            // Verifica se este ID está na lista de quem tem história
                            TemHistoria = meaningsComHistoria.Contains(m.Id)
                        }).ToList()
                    }).ToList(),

                // --- NOVO: Preencher a lista de Readings ---
                ListaReadings = kanji.KanjiReadings.Select(r => new ReadingDto
                    {
                        Id = r.Id,
                        Type = r.Type, // onyomi/kunyomi
                        Kana = r.ReadingKana,
                        Romaji = r.ReadingRomaji, // Assumindo que existe essa coluna, senão remova
                        IsPrincipal = r.IsPrincipal ?? false,
                        TemHistoria = readingsComHistoria.Contains(r.Id)
                    }).OrderBy(r => r.Type).ThenBy(r => r.Kana).ToList()

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
        // 5. MÉTODOS DE SALVAR E EXCLUIR TRADUÇÃO (Mantidos)
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
                    if (jaExiste) continue; // Evita erro se tentar salvar duplicado

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
            try
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
            catch (Exception ex) { return BadRequest(ex.Message); }
        }
        // ==========================================================
        // 6. CARREGAR HISTÓRIA (NOVO ENDPOINT)
        // ==========================================================
        [HttpGet]
        public async Task<IActionResult> GetHistoria(int meaningId)
        {
            // Busca a história ativa para este significado
            var historia = await _context.KanjiMeaningMnemonics
                .AsNoTracking()
                .Where(m => m.KanjiMeaningId == meaningId && m.IsActive)
                .Select(m => m.Text)
                .FirstOrDefaultAsync();

            // Retorna o texto (ou vazio se não achar)
            return Ok(new { texto = historia ?? "" });
        }

        [HttpPost]
        public async Task<IActionResult> SalvarHistoriaReading([FromBody] SalvarReadingRequest request)
        {
            if (request == null || request.ReadingId <= 0) return BadRequest("Dados inválidos.");
            if (string.IsNullOrWhiteSpace(request.Texto)) return BadRequest("Texto vazio.");

            try
            {
                // A. Atualizar flag IsPrincipal no Reading
                var reading = await _context.KanjiReadings.FindAsync(request.ReadingId);
                if (reading != null)
                {
                    // Opcional: Se quiser que só exista UM principal por Kanji, teria que resetar os outros antes
                    // Por enquanto, vamos apenas marcar este como principal conforme solicitado
                    reading.IsPrincipal = true;
                    _context.Update(reading);
                }

                // B. Versionamento da História (Igual ao Meaning)
                var antigas = await _context.KanjiReadingMnemonics
                    .Where(x => x.KanjiReadingId == request.ReadingId && x.IsActive)
                    .ToListAsync();

                foreach (var a in antigas)
                {
                    a.IsActive = false;
                    a.UpdatedAt = DateTime.UtcNow;
                }

                // C. Salvar Nova História
                var nova = new KanjiReadingMnemonic
                {
                    KanjiReadingId = request.ReadingId,
                    Text = request.Texto,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.KanjiReadingMnemonics.Add(nova);

                await _context.SaveChangesAsync();
                return Ok(new { mensagem = "Reading salvo com sucesso!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro: {ex.Message}");
            }
        }

        // DTO Auxiliar
        public class SalvarReadingRequest
        {
            public int ReadingId { get; set; }
            public string Texto { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetHistoriaReading(int readingId)
        {
            var historia = await _context.KanjiReadingMnemonics
                .AsNoTracking()
                .Where(x => x.KanjiReadingId == readingId && x.IsActive)
                .Select(x => x.Text)
                .FirstOrDefaultAsync();

            return Ok(new { texto = historia ?? "" });
        }

        // ==========================================================
        // 7. EXCLUIR HISTÓRIA DE READING
        // ==========================================================
        [HttpPost]
        public async Task<IActionResult> ExcluirHistoriaReading(int readingId)
        {
            try
            {
                var historicasAtivas = await _context.KanjiReadingMnemonics
                    .Where(x => x.KanjiReadingId == readingId && x.IsActive)
                    .ToListAsync();

                if (!historicasAtivas.Any()) return NotFound("Nenhuma história ativa encontrada.");

                foreach (var h in historicasAtivas)
                {
                    h.IsActive = false; // Soft delete
                    h.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return Ok(new { mensagem = "História removida." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro: {ex.Message}");
            }
        }

    }
}