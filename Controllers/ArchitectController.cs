using Denkiishi_v2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Denkiishi_v2.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ArchitectController : Controller
    {
        private readonly InasDbContext _context;

        public class UpdateOrderRequest
        {
            public int CircleId { get; set; }
            public List<int> MappingIds { get; set; }
        }
        public ArchitectController(InasDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Carrega Mandalas e Círculos
            var mandalas = await _context.Mandalas.OrderBy(m => m.Sequential).ToListAsync();
            var circles = await _context.Circles.OrderBy(c => c.Sequential).ToListAsync();

            // 2. Busca os Itens mapeados com SQL Nativo para velocidade
            var itens = new List<ItemDto>();
            string sql = @"
               	SELECT 
                    cui.id, cui.circle_id,
                    cui.kanji_id, k.literal as kanji_text,
                    cui.radical_id, r.literal as radical_text, 
                    cui.vocabulary_id, v.characters as vocab_text
                FROM circle_ue_item cui
                LEFT JOIN kanji k ON cui.kanji_id = k.id
                LEFT JOIN radical r ON cui.radical_id = r.id
                LEFT JOIN vocabulary v ON cui.vocabulary_id = v.id
                ORDER BY cui.sequential ASC
            ";

            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sql;
                await _context.Database.OpenConnectionAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var dto = new ItemDto
                        {
                            IdMapping = reader.GetInt32(0),
                            CircleId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1)
                        };

                        // Verifica qual tipo de item é (Kanji, Radical ou Vocab)
                        if (!reader.IsDBNull(2)) // É Kanji
                        {
                            dto.Tipo = "kanji";
                            dto.OriginalId = reader.GetInt32(2);
                            dto.Texto = reader.IsDBNull(3) ? "?" : reader.GetString(3);
                        }
                        else if (!reader.IsDBNull(4)) // É Radical
                        {
                            dto.Tipo = "radical";
                            dto.OriginalId = reader.GetInt32(4);
                            dto.Texto = reader.IsDBNull(5) ? "?" : reader.GetString(5);
                        }
                        else if (!reader.IsDBNull(6)) // É Vocab
                        {
                            dto.Tipo = "vocabulario";
                            dto.OriginalId = reader.GetInt32(6);
                            dto.Texto = reader.IsDBNull(7) ? "?" : reader.GetString(7);
                        }

                        itens.Add(dto);
                    }
                }
            }
            // BUSCA A COMPOSIÇÃO DOS KANJIS PARA O TOOLTIP
            // ==============================================================
            var kanjiComponents = new Dictionary<int, List<string>>();
            string sqlComps = @"
                SELECT 
                    kr.kanji_id, 
                    r.literal as rad_text, 
                    c.sequential as circle_seq
                FROM kanji_radical kr
                JOIN radical r ON kr.radical_id = r.id
                LEFT JOIN circle_ue_item cui ON cui.radical_id = r.id
                LEFT JOIN circle c ON c.id = cui.circle_id
            ";

            using (var command2 = _context.Database.GetDbConnection().CreateCommand())
            {
                command2.CommandText = sqlComps;
                if (command2.Connection.State != System.Data.ConnectionState.Open)
                {
                    await command2.Connection.OpenAsync();
                }

                using (var reader2 = await command2.ExecuteReaderAsync())
                {
                    while (await reader2.ReadAsync())
                    {
                        int kId = reader2.GetInt32(0);

                        // O IsDBNull protege contra erros caso o banco não tenha o caractere
                        string rText = reader2.IsDBNull(1) ? "?" : reader2.GetString(1);
                        string cSeq = reader2.IsDBNull(2) ? "?" : reader2.GetInt32(2).ToString();

                        if (!kanjiComponents.ContainsKey(kId))
                        {
                            kanjiComponents[kId] = new List<string>();
                        }

                        // Removemos as classes do Bootstrap e usamos CSS puro e bruto para garantir a cor preta e tamanho maior!
                        kanjiComponents[kId].Add($"<span style='background-color:#ffc107; color:#000 !important; font-size:14px; font-weight:bold; padding:2px 6px; border-radius:4px; margin-right:2px;'>{rText}</span><span style='color:#ccc; font-size:11px; margin-right:8px;'>(C{cSeq})</span>");
                    }
                }
            }

            // Atribui as composições aos Kanjis correspondentes na nossa lista
            foreach (var item in itens)
            {
                if (item.Tipo == "kanji" && kanjiComponents.ContainsKey(item.OriginalId))
                {
                    item.TooltipExtra = string.Join(" ", kanjiComponents[item.OriginalId]);
                }
            }

            // 3. Monta a Estrutura Base
            var viewModel = new ArchitectViewModel();

            // ==============================================================
            // BUSCA DE LÍNGUAS E SIGNIFICADOS PARA O FILTRO (KANJI, RADICAL, VOCAB)
            // ==============================================================
            var linguas = await _context.Language.OrderBy(l => l.Description).ToListAsync();
            viewModel.LinguasDisponiveis = linguas.Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Description }).ToList();
            viewModel.LinguaPadraoId = linguas.FirstOrDefault(l => l.Description.Contains("Português") || l.Description.Contains("Portuguese"))?.Id ?? (linguas.Any() ? linguas.First().Id : 1);

            // Dicionário mestre: "tipo_idItem" -> [idLingua -> lista de significados]
            var dictMeanings = new Dictionary<string, Dictionary<int, List<string>>>();

            // 1. Significados dos Kanjis
            var kanjiMeanings = await _context.Set<KanjiMeaning>().ToListAsync();
            foreach (var km in kanjiMeanings)
            {
                int lId = km.IdLanguage ?? 0;
                if (lId == 0) continue;
                string key = "kanji_" + km.KanjiId;
                if (!dictMeanings.ContainsKey(key)) dictMeanings[key] = new Dictionary<int, List<string>>();
                if (!dictMeanings[key].ContainsKey(lId)) dictMeanings[key][lId] = new List<string>();
                dictMeanings[key][lId].Add(km.Gloss);
            }

            // 2. Significados dos Vocabulários
            var vocabMeanings = await _context.Set<VocabularyMeaning>().ToListAsync();
            foreach (var vm in vocabMeanings)
            {
                int lId = vm.LanguageId ;
                if (lId == 0) continue;
                string key = "vocabulario_" + vm.VocabularyId;
                if (!dictMeanings.ContainsKey(key)) dictMeanings[key] = new Dictionary<int, List<string>>();
                if (!dictMeanings[key].ContainsKey(lId)) dictMeanings[key][lId] = new List<string>();
                dictMeanings[key][lId].Add(vm.Meaning);
            }

            // 3. Significados dos Radicais (Lendo a tabela radical_meaning)
            var radicalMeanings = await _context.Set<RadicalMeaning>().ToListAsync();
            foreach (var rm in radicalMeanings)
            {
                int lId = rm.IdLanguage ?? 0;
                if (lId == 0) continue;
                string key = "radical_" + rm.IdRadical;
                if (!dictMeanings.ContainsKey(key)) dictMeanings[key] = new Dictionary<int, List<string>>();
                if (!dictMeanings[key].ContainsKey(lId)) dictMeanings[key][lId] = new List<string>();

                // Usamos "Descrition" porque é o nome exato da coluna no seu script SQL
                dictMeanings[key][lId].Add(rm.Description);
            }
            // ==============================================================
            // BUSCA DO STATUS DE CONCLUSÃO (CHECK VERDE)
            // ==============================================================
            var kanjiWithReading = new HashSet<int>();
            var kanjiMeaningComplete = new Dictionary<int, HashSet<int>>();
            var radicalComplete = new Dictionary<int, HashSet<int>>();

            // Super Query ADO.NET para extrair as 3 informações de uma vez só!
            string sqlCheck = @"
                -- 1. Kanjis com Mnemônico de Leitura Ativo
                SELECT DISTINCT kr.kanji_id
                FROM kanji_reading kr
                JOIN kanji_reading_mnemonic krm ON kr.id = krm.kanji_reading_id
                WHERE krm.is_active = true;

                -- 2. Kanjis com Mnemônico de Significado Ativo (Por Língua)
                SELECT km.kanji_id, km.id_language
                FROM kanji_meaning km
                JOIN kanji_meaning_mnemonic kmm ON km.id = kmm.kanji_meaning_id
                WHERE kmm.is_active = true
                GROUP BY km.kanji_id, km.id_language;

                -- 3. Radicais com Mnemônico de Significado Ativo (Por Língua)
                SELECT rm.id_radical, rm.id_language
                FROM radical_meaning rm
                JOIN radical_meaning_mnemonic rmm ON rm.id = rmm.radical_meaning_id
                WHERE rmm.is_active = true
                GROUP BY rm.id_radical, rm.id_language;
            ";

            using (var cmd = _context.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = sqlCheck;
                if (cmd.Connection.State != System.Data.ConnectionState.Open) await cmd.Connection.OpenAsync();

                using (var readerCheck = await cmd.ExecuteReaderAsync())
                {
                    // Lendo Bloco 1 (Leituras Kanji)
                    while (await readerCheck.ReadAsync()) kanjiWithReading.Add(readerCheck.GetInt32(0));

                    // Lendo Bloco 2 (Significados Kanji)
                    if (await readerCheck.NextResultAsync())
                    {
                        while (await readerCheck.ReadAsync())
                        {
                            int kId = readerCheck.GetInt32(0);
                            int lId = readerCheck.IsDBNull(1) ? 0 : readerCheck.GetInt32(1);
                            if (!kanjiMeaningComplete.ContainsKey(kId)) kanjiMeaningComplete[kId] = new HashSet<int>();
                            kanjiMeaningComplete[kId].Add(lId);
                        }
                    }

                    // Lendo Bloco 3 (Significados Radical)
                    if (await readerCheck.NextResultAsync())
                    {
                        while (await readerCheck.ReadAsync())
                        {
                            int rId = readerCheck.GetInt32(0);
                            int lId = readerCheck.IsDBNull(1) ? 0 : readerCheck.GetInt32(1);
                            if (!radicalComplete.ContainsKey(rId)) radicalComplete[rId] = new HashSet<int>();
                            radicalComplete[rId].Add(lId);
                        }
                    }
                }
            }
            // ==============================================================

            // Anexa as traduções aos Itens que vão para a tela
            // Anexa as traduções aos Itens que vão para a tela
            foreach (var item in itens)
            {
                string dictKey = item.Tipo + "_" + item.OriginalId;
                if (dictMeanings.ContainsKey(dictKey))
                {
                    foreach (var kvp in dictMeanings[dictKey])
                    {
                        item.Meanings[kvp.Key] = string.Join(", ", kvp.Value);
                    }
                }

                // NOVO: Calcula se o item está 100% completo em cada língua!
                foreach (var lang in linguas)
                {
                    bool isComplete = false;
                    if (item.Tipo == "radical")
                    {
                        // Radical só precisa de significado na língua
                        isComplete = radicalComplete.ContainsKey(item.OriginalId) && radicalComplete[item.OriginalId].Contains(lang.Id);
                    }
                    else if (item.Tipo == "kanji")
                    {
                        // Kanji precisa ter a Leitura E o Significado na língua
                        bool hasReading = kanjiWithReading.Contains(item.OriginalId);
                        bool hasMeaning = kanjiMeaningComplete.ContainsKey(item.OriginalId) && kanjiMeaningComplete[item.OriginalId].Contains(lang.Id);
                        isComplete = hasReading && hasMeaning;
                    }
                    // (O Vocabulário fica para o futuro, deixamos false por padrão)

                    item.IsComplete[lang.Id] = isComplete;
                }
            }
            // ==============================================================

            // 4. Constrói as Mandalas e Círculos
            foreach (var m in mandalas)
            {
                var mandalaDto = new MandalaDto
                {
                    Id = m.Id,
                    Nome = m.Text,
                    Ordem = m.Sequential
                };

                var circulosDaMandala = circles.Where(c => c.MandalaId == m.Id).ToList();
                foreach (var c in circulosDaMandala)
                {
                    var circleDto = new CircleDto
                    {
                        Id = c.Id,
                        Nome = c.Text,
                        Ordem = c.Sequential,
                        Itens = itens.Where(i => i.CircleId == c.Id).ToList()
                    };
                    mandalaDto.Circulos.Add(circleDto);
                }

                viewModel.Mandalas.Add(mandalaDto);
            }

            return View(viewModel);
        }
        // ==========================================
        // FASE 3: MOTOR DE REGRAS E PERSISTÊNCIA
        // ==========================================

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> UpdateCircleOrder([FromBody] UpdateOrderRequest request)
        {
            if (request == null || request.MappingIds == null) return BadRequest();

            // Atualiza o Círculo e a Sequência de todos os itens daquele Círculo de uma vez!
            for (int i = 0; i < request.MappingIds.Count; i++)
            {
                var item = await _context.CircleUeItems.FindAsync(request.MappingIds[i]);
                if (item != null)
                {
                    item.CircleId = request.CircleId;
                    item.Sequential = i + 1; // Grava a posição exata (1º, 2º, 3º...)
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> ValidateMatrix()
        {
            var errors = new List<dynamic>();

            using (var cmd = _context.Database.GetDbConnection().CreateCommand())
            {
                if (cmd.Connection.State != System.Data.ConnectionState.Open)
                    await cmd.Connection.OpenAsync();

                // Usamos UNION ALL para validar Kanjis e Vocabs.
                // Criámos o "globalReqSeq" para garantir que a comparação de distância funciona entre Mandalas diferentes!
                cmd.CommandText = @"
                    -- 1. KANJIS vs RADICAIS
                    SELECT 
                        'kanji' AS type,
                        k_item.id AS mappingId,
                        k.literal AS itemText,
                        c_k.sequential AS itemSeq,
                        r.literal AS reqText,
                        c_r.sequential AS reqSeq,
                        c_r.id AS suggestedCircleId,
                        COALESCE((m_r.sequential * 1000) + c_r.sequential, 999999) AS globalReqSeq
                    FROM circle_ue_item k_item
                    JOIN kanji k ON k_item.kanji_id = k.id
                    JOIN circle c_k ON k_item.circle_id = c_k.id
                    JOIN mandala m_k ON c_k.mandala_id = m_k.id
                    JOIN kanji_radical kr ON k.id = kr.kanji_id  /* AQUI ESTAVA O ERRO (Corrigido para kanji_radical) */
                    JOIN radical r ON kr.radical_id = r.id
                    LEFT JOIN circle_ue_item r_item ON r.id = r_item.radical_id
                    LEFT JOIN circle c_r ON r_item.circle_id = c_r.id
                    LEFT JOIN mandala m_r ON c_r.mandala_id = m_r.id
                    WHERE 
                        (r_item.id IS NULL) 
                        OR (m_k.sequential < m_r.sequential) 
                        OR (m_k.sequential = m_r.sequential AND c_k.sequential < c_r.sequential)

                    UNION ALL

                    -- 2. VOCABULÁRIOS vs KANJIS
                    SELECT 
                        'vocab' AS type,
                        v_item.id AS mappingId,
                        v.characters AS itemText,
                        c_v.sequential AS itemSeq,
                        k.literal AS reqText,
                        c_k.sequential AS reqSeq,
                        c_k.id AS suggestedCircleId,
                        COALESCE((m_k.sequential * 1000) + c_k.sequential, 999999) AS globalReqSeq
                    FROM circle_ue_item v_item
                    JOIN vocabulary v ON v_item.vocabulary_id = v.id
                    JOIN circle c_v ON v_item.circle_id = c_v.id
                    JOIN mandala m_v ON c_v.mandala_id = m_v.id
                    JOIN vocabulary_composition vc ON v.id = vc.vocabulary_id
                    JOIN kanji k ON vc.kanji_id = k.id
                    LEFT JOIN circle_ue_item k_item ON k.id = k_item.kanji_id
                    LEFT JOIN circle c_k ON k_item.circle_id = c_k.id
                    LEFT JOIN mandala m_k ON c_k.mandala_id = m_k.id
                    WHERE 
                        (k_item.id IS NULL) 
                        OR (m_v.sequential < m_k.sequential) 
                        OR (m_v.sequential = m_k.sequential AND c_v.sequential < c_k.sequential)
                ";

                var combinedErrors = new Dictionary<int, dynamic>();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string type = reader.GetString(0);
                        int mappingId = reader.GetInt32(1);
                        string itemText = reader.GetString(2);
                        int itemSeq = reader.GetInt32(3);
                        string reqText = reader.GetString(4);
                        int? reqSeq = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                        int? suggestedCircleId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
                        int globalReqSeq = reader.GetInt32(7);

                        if (!combinedErrors.ContainsKey(mappingId))
                        {
                            combinedErrors[mappingId] = new
                            {
                                type = type,
                                mappingId = mappingId,
                                itemText = itemText,
                                itemSeq = itemSeq,
                                requirements = new List<string>(),
                                suggestedSeq = reqSeq ?? 999,
                                suggestedCircleId = suggestedCircleId ?? 0,
                                maxGlobalSeq = globalReqSeq // Guarda a distância máxima
                            };
                        }

                        combinedErrors[mappingId].requirements.Add(reqText);

                        // Garante que o botão de auto-correção aponte sempre para o requisito mais avançado
                        if (globalReqSeq > combinedErrors[mappingId].maxGlobalSeq)
                        {
                            combinedErrors[mappingId] = new
                            {
                                type = type,
                                mappingId = mappingId,
                                itemText = itemText,
                                itemSeq = itemSeq,
                                requirements = combinedErrors[mappingId].requirements,
                                suggestedSeq = reqSeq ?? 999,
                                suggestedCircleId = suggestedCircleId ?? 0,
                                maxGlobalSeq = globalReqSeq
                            };
                        }
                    }
                }

                // Mapeia para as variáveis exatas que o seu Javascript já conhece
                foreach (var err in combinedErrors.Values)
                {
                    errors.Add(new
                    {
                        type = err.type,
                        mappingId = err.mappingId,
                        kanjiText = err.itemText,
                        kanjiSeq = err.itemSeq,
                        radicals = string.Join(", ", err.requirements),
                        suggestedSeq = err.suggestedSeq,
                        suggestedCircleId = err.suggestedCircleId
                    });
                }
            }

            return Json(errors);
        }
    }

}