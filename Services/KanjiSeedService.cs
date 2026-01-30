
using Denkiishi_v2.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization; // Importante para o [JsonPropertyName]

namespace Denkiishi_v2.Services
{
    public class KanjiSeedService
    {
        private readonly InasDbContext _context;

        public KanjiSeedService(InasDbContext context)
        {
            _context = context;
        }

        public async Task ImportarDadosTanosAsync(string jsonContent)
        {
            // O Deserialize precisa saber para qual classe converter (KanjiJsonDto)
            var dadosExternos = JsonSerializer.Deserialize<Dictionary<string, KanjiJsonDto>>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dadosExternos == null) return;

            // 1. Garante Categoria JLPT e Idioma
            var catJLPT = await GetOrCreateCategory("JLPT", "Japanese Language Proficiency Test");
            // var catJouyou = await GetOrCreateCategory("Jouyou", "Kanjis de uso comum"); // (Opcional por enquanto)
            var linguaEn = await GetOrCreateLanguage("en", "English");

            foreach (var item in dadosExternos)
            {
                string caractere = item.Key;
                var dados = item.Value;

                var kanjiBanco = await _context.Kanjis
                    .Include(k => k.KanjiCategoryMaps)
                    .FirstOrDefaultAsync(k => k.Literal == caractere);

                if (kanjiBanco != null)
                {
                    // --- UPDATE COM LOG ---
                    var changes = new Dictionary<string, object>();

                    // Verifica se mudou traços
                    if (dados.Strokes > 0 && kanjiBanco.StrokeCount != dados.Strokes)
                    {
                        changes.Add("StrokeCount", new { old = kanjiBanco.StrokeCount, @new = dados.Strokes });
                        kanjiBanco.StrokeCount = (short)dados.Strokes;
                    }

                    // Verifica/Atualiza Categoria JLPT
                    if (dados.JlptNew.HasValue)
                    {
                        await AtualizarCategoria(kanjiBanco, catJLPT.Id, $"N{dados.JlptNew}", changes);
                    }

                    // Salva Log se houve mudança
                    if (changes.Any())
                    {
                        _context.KanjiAuditLogs.Add(new KanjiAuditLog
                        {
                            KanjiId = kanjiBanco.Id,
                            ActionType = "UPDATE",
                            Source = "Tanos Import",
                            ChangedFields = JsonSerializer.Serialize(changes)
                        });
                        _context.Kanjis.Update(kanjiBanco);
                    }
                }
                else
                {
                    // --- INSERT ---
                    var novoKanji = new Kanji
                    {
                        Literal = caractere,
                        UnicodeCode = ((int)caractere[0]).ToString("X4"),
                        StrokeCount = (short)dados.Strokes,
                        FrequencyRank = dados.Freq,
                        IsActive = true
                    };

                    _context.Kanjis.Add(novoKanji);
                    await _context.SaveChangesAsync();

                    // Adicionar Categoria JLPT
                    if (dados.JlptNew.HasValue)
                    {
                        _context.KanjiCategoryMaps.Add(new KanjiCategoryMap
                        {
                            KanjiId = novoKanji.Id,
                            CategoryId = catJLPT.Id,
                            CategoryLevel = $"N{dados.JlptNew}",
                            InclDate = DateTime.UtcNow
                        });
                    }

                    // Log de Criação
                    _context.KanjiAuditLogs.Add(new KanjiAuditLog
                    {
                        KanjiId = novoKanji.Id,
                        ActionType = "INSERT",
                        Source = "Tanos Import",
                        ChangedFields = "{ \"status\": \"Created\" }"
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        // --- Helpers ---
        private async Task<Category> GetOrCreateCategory(string name, string desc)
        {
            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == name);
            if (cat == null)
            {
                cat = new Category { Name = name, Description = desc };
                _context.Categories.Add(cat);
                await _context.SaveChangesAsync();
            }
            return cat;
        }

        private async Task<Language> GetOrCreateLanguage(string code, string desc)
        {
            var lang = await _context.Language.FirstOrDefaultAsync(l => l.LanguageCode == code);
            if (lang == null)
            {
                lang = new Language { LanguageCode = code, Description = desc, IsActive = true };
                _context.Language.Add(lang);
                await _context.SaveChangesAsync();
            }
            return lang;
        }

        private async Task AtualizarCategoria(Kanji kanji, int catId, string novoNivel, Dictionary<string, object> changes)
        {
            var map = kanji.KanjiCategoryMaps.FirstOrDefault(c => c.CategoryId == catId);
            if (map == null)
            {
                // Não tinha JLPT, agora tem
                _context.KanjiCategoryMaps.Add(new KanjiCategoryMap { KanjiId = kanji.Id, CategoryId = catId, CategoryLevel = novoNivel, InclDate = DateTime.UtcNow });
                changes.Add("JLPT", new { old = "null", @new = novoNivel });
            }
            else if (map.CategoryLevel != novoNivel)
            {
                // Mudou de nível (ex: N4 -> N3)
                changes.Add("JLPT", new { old = map.CategoryLevel, @new = novoNivel });
                map.CategoryLevel = novoNivel;
                map.InclDate = DateTime.UtcNow; // Atualiza data da mudança de categoria
            }
        }
    }

    // --- AQUI ESTÁ A CLASSE QUE FALTAVA! ---
    public class KanjiJsonDto
    {
        [JsonPropertyName("strokes")]
        public int Strokes { get; set; }

        [JsonPropertyName("grade")]
        public int? Grade { get; set; }

        [JsonPropertyName("freq")]
        public int? Freq { get; set; }

        [JsonPropertyName("jlpt_new")]
        public int? JlptNew { get; set; }

        [JsonPropertyName("meanings")]
        public List<string>? Meanings { get; set; }

        [JsonPropertyName("readings_on")]
        public List<string>? ReadingsOn { get; set; }

        [JsonPropertyName("readings_kun")]
        public List<string>? ReadingsKun { get; set; }
    }
}