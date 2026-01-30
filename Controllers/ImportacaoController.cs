using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Denkiishi_v2.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace Denkiishi_v2.Controllers
{
    // --- DTO para mapear o JSON do David Luz ---
    public class DavidLuzKanjiDto
    {
        [JsonPropertyName("strokes")]
        public int Strokes { get; set; }

        [JsonPropertyName("grade")]
        public int? Grade { get; set; }

        [JsonPropertyName("freq")]
        public int? Freq { get; set; }

        [JsonPropertyName("jlpt_new")]
        public int? JlptNew { get; set; } // Usa o nível novo (N5-N1)

        [JsonPropertyName("meanings")]
        public List<string>? Meanings { get; set; }

        [JsonPropertyName("readings_on")]
        public List<string>? ReadingsOn { get; set; }

        [JsonPropertyName("readings_kun")]
        public List<string>? ReadingsKun { get; set; }
    }

    public class ImportacaoController : Controller
    {
        private readonly InasDbContext _context;

        public ImportacaoController(InasDbContext context)
        {
            _context = context;
        }

        // 1. Tela Inicial (GET)
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // 2. Ação de Importação (POST)
        [HttpPost]
        public async Task<IActionResult> ImportarTanos(IFormFile arquivoJson)
        {
            if (arquivoJson == null || arquivoJson.Length == 0)
                return BadRequest("Por favor, seleciona um arquivo JSON válido.");

            // 1. Ler o JSON como um Dicionário
            using var stream = new StreamReader(arquivoJson.OpenReadStream());
            var conteudoJson = await stream.ReadToEndAsync();

            Dictionary<string, DavidLuzKanjiDto>? dadosImportacao;
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                dadosImportacao = JsonSerializer.Deserialize<Dictionary<string, DavidLuzKanjiDto>>(conteudoJson, options);
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro ao ler o JSON: {ex.Message}");
            }

            if (dadosImportacao == null || !dadosImportacao.Any())
                return BadRequest("O JSON está vazio.");

            // 2. Preparar Dados Auxiliares (Categorias Mestre e Idioma)
            // Agora garantimos apenas a categoria PAI "JLPT"
            var categoriaJlpt = await GarantirCategoriaMestre("JLPT", "Japanese Language Proficiency Test");

            // Garante idioma Inglês (assumindo ID 1 para simplificar, ajuste conforme seu DB)
            int idLanguageEn = 1;

            int adicionados = 0;
            int atualizados = 0;

            // 3. Processar cada entrada do Dicionário
            foreach (var entry in dadosImportacao)
            {
                string literal = entry.Key;     // O Kanji (Ex: "日")
                var dados = entry.Value;        // Os dados (Strokes, Grade, etc)

                // Define o Nível (Ex: 5 -> "N5")
                string? nivelJlpt = dados.JlptNew.HasValue ? $"N{dados.JlptNew}" : null;

                var kanjiExistente = await _context.Kanjis
                    .FirstOrDefaultAsync(k => k.Literal == literal);

                if (kanjiExistente == null)
                {
                    // --- INSERT (Novo Kanji) ---
                    var novoKanji = new Kanji
                    {
                        Literal = literal,
                        UnicodeCode = char.ConvertToUtf32(literal, 0).ToString("X4"),
                        StrokeCount = (short)dados.Strokes,
                        GradeLevel = (short?)dados.Grade,
                        FrequencyRank = dados.Freq,
                        IsActive = true
                    };

                    _context.Kanjis.Add(novoKanji);
                    await _context.SaveChangesAsync(); // Salva para gerar ID

                    // Vincular Categoria JLPT (Forma Correta: Categoria=JLPT, Nível=N5)
                    if (nivelJlpt != null)
                    {
                        await VincularCategoria(novoKanji.Id, categoriaJlpt.Id, nivelJlpt);
                    }

                    // Inserir Significados (Meanings)
                    if (dados.Meanings != null)
                    {
                        foreach (var m in dados.Meanings)
                        {
                            await _context.Database.ExecuteSqlRawAsync(
                                "INSERT INTO kanji_meaning (kanji_id, gloss, id_language) VALUES ({0}, {1}, {2})",
                                novoKanji.Id, m, idLanguageEn);
                        }
                    }

                    // Inserir Leituras (Readings)
                    if (dados.ReadingsOn != null)
                    {
                        foreach (var r in dados.ReadingsOn)
                            await _context.Database.ExecuteSqlRawAsync(
                                "INSERT INTO kanji_reading (kanji_id, type, reading_kana) VALUES ({0}, 'ONYOMI', {1})",
                                novoKanji.Id, r);
                    }
                    if (dados.ReadingsKun != null)
                    {
                        foreach (var r in dados.ReadingsKun)
                            await _context.Database.ExecuteSqlRawAsync(
                                "INSERT INTO kanji_reading (kanji_id, type, reading_kana) VALUES ({0}, 'KUNYOMI', {1})",
                                novoKanji.Id, r);
                    }

                    await RegistrarLog(novoKanji.Id, "INSERT", "JSON Import", "{}");
                    adicionados++;
                }
                else
                {
                    // --- UPDATE (Kanji Existente) ---
                    var mudancas = new Dictionary<string, object>();
                    bool houveMudanca = false;

                    // Lógica de Atualização da Categoria JLPT
                    if (nivelJlpt != null)
                    {
                        // Busca se JÁ existe vínculo com a categoria JLPT
                        var vinculoExistente = await _context.KanjiCategoryMaps
                            .FirstOrDefaultAsync(m => m.KanjiId == kanjiExistente.Id && m.CategoryId == categoriaJlpt.Id);

                        if (vinculoExistente == null)
                        {
                            // Não tinha JLPT, adiciona agora
                            await VincularCategoria(kanjiExistente.Id, categoriaJlpt.Id, nivelJlpt);
                            mudancas.Add("Category", $"Adicionado JLPT {nivelJlpt}");
                            houveMudanca = true;
                        }
                        else if (vinculoExistente.CategoryLevel != nivelJlpt)
                        {
                            // Já tinha, mas mudou de nível (Ex: N3 -> N2)
                            string antigo = vinculoExistente.CategoryLevel;
                            vinculoExistente.CategoryLevel = nivelJlpt;
                            mudancas.Add("CategoryLevel", $"Mudou de {antigo} para {nivelJlpt}");
                            houveMudanca = true;
                        }
                    }

                    // Atualiza Grade/Frequência se estiverem vazios
                    if (kanjiExistente.GradeLevel == null && dados.Grade.HasValue)
                    {
                        kanjiExistente.GradeLevel = (short?)dados.Grade;
                        mudancas.Add("Grade", dados.Grade);
                        houveMudanca = true;
                    }
                    if (kanjiExistente.FrequencyRank == null && dados.Freq.HasValue)
                    {
                        kanjiExistente.FrequencyRank = dados.Freq;
                        mudancas.Add("Freq", dados.Freq);
                        houveMudanca = true;
                    }

                    // Logar Alterações
                    if (houveMudanca)
                    {
                        string jsonLog = JsonSerializer.Serialize(mudancas);
                        await RegistrarLog(kanjiExistente.Id, "UPDATE", "JSON Import", jsonLog);
                        atualizados++;
                    }
                }
            }

            await _context.SaveChangesAsync();

            ViewBag.Mensagem = $"Processamento Concluído! {adicionados} novos Kanjis, {atualizados} atualizados.";
            return View("Index");
        }

        // 3. Ação para Gerar Script SQL (Ferramenta Admin)
        [HttpPost]
        public async Task<IActionResult> GerarScriptSql(IFormFile arquivoJson)
        {
            if (arquivoJson == null || arquivoJson.Length == 0)
                return BadRequest("Por favor, seleciona o arquivo JSON.");

            using var stream = new StreamReader(arquivoJson.OpenReadStream());
            var conteudo = await stream.ReadToEndAsync();

            Dictionary<string, object>? dados;
            try
            {
                dados = JsonSerializer.Deserialize<Dictionary<string, object>>(conteudo);
            }
            catch
            {
                return BadRequest("O arquivo não é um JSON válido.");
            }

            if (dados == null || !dados.Any())
                return BadRequest("JSON vazio.");

            var listaKanjis = dados.Keys.ToList();
            var sb = new StringBuilder();
            sb.AppendLine("-- Script Gerado Automaticamente pelo Denkiishi v2");
            sb.AppendLine($"-- Data: {DateTime.Now}");
            sb.AppendLine($"-- Total de Kanjis Identificados: {listaKanjis.Count}");
            sb.AppendLine();

            var listaFormatada = string.Join("', '", listaKanjis);
            sb.AppendLine($"UPDATE kanji SET is_active = true WHERE literal IN ('{listaFormatada}');");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "application/sql", "Update_Kanjis_Active.sql");
        }

        // --- MÉTODOS AUXILIARES ---

        private async Task<Category> GarantirCategoriaMestre(string nome, string descricao)
        {
            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == nome);
            if (cat == null)
            {
                cat = new Category { Name = nome, Description = descricao };
                _context.Categories.Add(cat);
                await _context.SaveChangesAsync();
            }
            return cat;
        }

        private async Task VincularCategoria(int kanjiId, int catId, string levelName)
        {
            var vinculo = new KanjiCategoryMap
            {
                KanjiId = kanjiId,
                CategoryId = catId,
                CategoryLevel = levelName,
                InclDate = DateTime.UtcNow
            };
            _context.KanjiCategoryMaps.Add(vinculo);
        }

        private async Task RegistrarLog(int kanjiId, string action, string source, string jsonChanges)
        {
            var log = new KanjiAuditLog
            {
                KanjiId = kanjiId,
                ActionType = action,
                Source = source,
                ChangedFields = jsonChanges,
                CreatedAt = DateTime.UtcNow
            };
            _context.KanjiAuditLogs.Add(log);
        }
    }
}