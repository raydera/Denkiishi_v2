using Microsoft.AspNetCore.Mvc;
using Denkiishi_v2.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Denkiishi_v2.Controllers
{
    public class ImportacaoController : Controller
    {
        private readonly InasDbContext _context;
        private readonly ILogger<ImportacaoController> _logger;

        public ImportacaoController(InasDbContext context, ILogger<ImportacaoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SincronizarJishoJson()
        {
            // Reduzi o lote para 5 para testarmos se o salvamento funciona
            // Depois podemos aumentar para 20
            int tamanhoLote = 5;

            var idsProcessados = await _context.VocabularyPartsOfSpeechMaps
                .Select(m => m.VocabularyId)
                .Distinct()
                .ToListAsync();

            var loteVocabulario = await _context.Vocabularies
                .Where(v => !idsProcessados.Contains(v.Id))
                .OrderBy(v => v.Id)
                .Take(tamanhoLote)
                .ToListAsync();

            int totalPendentes = await _context.Vocabularies.CountAsync(v => !idsProcessados.Contains(v.Id));
            int totalGeral = await _context.Vocabularies.CountAsync();

            if (!loteVocabulario.Any())
            {
                return Json(new { finalizado = true, mensagem = "Processo concluído!", progresso = 100 });
            }

            int processadosNoLote = 0;
            var logItens = new List<string>();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "DenkiishiApp/2.0");
                client.Timeout = TimeSpan.FromSeconds(60);

                foreach (var vocab in loteVocabulario)
                {
                    // Limpa o ChangeTracker para não acumular lixo de erros anteriores
                    _context.ChangeTracker.Clear();

                    try
                    {
                        string url = $"https://jisho.org/api/v1/search/words?keyword={vocab.Characters}";
                        var response = await client.GetAsync(url);

                        if (response.IsSuccessStatusCode)
                        {
                            var jsonString = await response.Content.ReadAsStringAsync();
                            var jishoResult = JsonSerializer.Deserialize<JishoRoot>(jsonString);
                            var itemDados = jishoResult?.Data?.FirstOrDefault();

                            var tiposParaVincular = new List<string>();

                            if (itemDados != null && itemDados.Senses != null)
                            {
                                var encontrados = itemDados.Senses
                                    .Where(s => s.PartsOfSpeech != null)
                                    .SelectMany(s => s.PartsOfSpeech)
                                    .Distinct()
                                    .ToList();

                                tiposParaVincular.AddRange(encontrados);
                            }

                            if (!tiposParaVincular.Any()) tiposParaVincular.Add("Unclassified");

                            // --- LÓGICA BLINDADA DE SALVAMENTO ---
                            foreach (var posName in tiposParaVincular)
                            {
                                if (string.IsNullOrWhiteSpace(posName)) continue;

                                try
                                {
                                    // 1. Garante que o TIPO existe
                                    var tipoBanco = await _context.VocabularyPartsOfSpeech
                                        .FirstOrDefaultAsync(p => p.Name == posName);

                                    if (tipoBanco == null)
                                    {
                                        tipoBanco = new VocabularyPartOfSpeech { Name = posName };
                                        _context.VocabularyPartsOfSpeech.Add(tipoBanco);
                                        await _context.SaveChangesAsync(); // Salva IMEDIATAMENTE para ter o ID
                                    }

                                    // 2. Garante que o VÍNCULO não existe
                                    // Importante: Usar AsNoTracking() na verificação para evitar conflito de tracking
                                    var existeVinculo = await _context.VocabularyPartsOfSpeechMaps
                                        .AsNoTracking()
                                        .AnyAsync(m => m.VocabularyId == vocab.Id && m.VocabularyPartOfSpeechId == tipoBanco.Id);

                                    if (!existeVinculo)
                                    {
                                        var novoMapa = new VocabularyPartOfSpeechMap
                                        {
                                            VocabularyId = vocab.Id,
                                            VocabularyPartOfSpeechId = tipoBanco.Id
                                        };
                                        _context.VocabularyPartsOfSpeechMaps.Add(novoMapa);
                                        await _context.SaveChangesAsync(); // Salva o vínculo um por um
                                    }
                                }
                                catch (Exception dbEx)
                                {
                                    // Captura o erro real do banco (InnerException)
                                    string erroReal = dbEx.InnerException?.Message ?? dbEx.Message;
                                    logItens.Add($"[ERRO DB] '{vocab.Characters}' ({posName}): {erroReal}");
                                }
                            }

                            processadosNoLote++;
                            logItens.Add($"[OK] {vocab.Characters} -> {string.Join(", ", tiposParaVincular)}");

                            // Pausa saudável: 1.5s
                            await Task.Delay(1500);
                        }
                        else if ((int)response.StatusCode == 429) // Too Many Requests
                        {
                            logItens.Add($"[429 - CALMA] O Jisho pediu pausa. Esperando 10s...");
                            await Task.Delay(10000); // Espera 10 segundos
                        }
                        else
                        {
                            logItens.Add($"[ERRO API] {vocab.Characters}: {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logItens.Add($"[FALHA GERAL] {vocab.Characters}: {ex.Message}");
                    }
                }
            }

            int restantes = totalPendentes - processadosNoLote;
            double porcentagem = totalGeral > 0 ? (double)(totalGeral - restantes) / totalGeral * 100 : 0;

            return Json(new
            {
                finalizado = false,
                processados = processadosNoLote,
                restantes = restantes,
                total = totalGeral,
                progresso = Math.Round(porcentagem, 2),
                log = logItens
            });
        }
    }

    // --- CLASSES AUXILIARES PARA LER O JSON DO JISHO ---
    // Estrutura baseada em: https://jisho.org/api/v1/search/words?keyword=一人

    public class JishoRoot
    {
        [JsonPropertyName("meta")]
        public JishoMeta Meta { get; set; }

        [JsonPropertyName("data")]
        public List<JishoData> Data { get; set; }
    }

    public class JishoMeta
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }
    }

    public class JishoData
    {
        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("senses")]
        public List<JishoSense> Senses { get; set; }
    }

    public class JishoSense
    {
        [JsonPropertyName("english_definitions")]
        public List<string> EnglishDefinitions { get; set; }

        [JsonPropertyName("parts_of_speech")]
        public List<string> PartsOfSpeech { get; set; }
    }
}