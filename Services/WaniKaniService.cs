using Denkiishi_v2.Models;
using Denkiishi_v2.Services.WaniKani.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Denkiishi_v2.Services
{
    public class WaniKaniService
    {
        private readonly InasDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        // Token (idealmente viria do appsettings.json)
        private const string ApiToken = "0f985f01-f9c2-4ea9-b05f-767c019a54be";

        // Classe auxiliar para memória
        private class PendenciaDecomposicao
        {
            public int LocalIdCriado { get; set; }
            public string Tipo { get; set; }
            public List<int> IdsComponentesWk { get; set; } = new();
        }

        public WaniKaniService(InasDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        private string GetUnicodeHex(string character)
        {
            if (string.IsNullOrEmpty(character)) return "U+0000";
            int codePoint = char.ConvertToUtf32(character, 0);
            return $"U+{codePoint:X4}";
        }

        // Método auxiliar para garantir que a língua "Inglês" existe no banco (Necessário para as FKs)
        private async Task<int> GetOrCreateLanguageId(string code, string name)
        {
            // Tenta achar pelo código (ex: "en")
            var lang = await _context.Language.FirstOrDefaultAsync(l => l.LanguageCode == code);

            if (lang != null) return lang.Id;

            // Se não existe, cria
            var newLang = new Language
            {
                LanguageCode = code,
                Description = name,
                IsActive = true
            };
            _context.Language.Add(newLang);
            await _context.SaveChangesAsync();
            return newLang.Id;
        }

        public async Task<string> ImportDataAsync()
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiToken);
            client.DefaultRequestHeaders.Add("Wanikani-Revision", "20170710");

            // URL Inicial
            string? nextUrl = "https://api.wanikani.com/v2/subjects?types=radical,kanji,vocabulary";

            var pendencias = new List<PendenciaDecomposicao>();
            int cRad = 0, cKan = 0, cVoc = 0;

            // 1. GARANTIR A LÍNGUA INGLÊS (Obrigatório para gravar significados)
            int idLangEn = await GetOrCreateLanguageId("en", "English");

            // --- FASE 1: IMPORTAÇÃO ---
            while (!string.IsNullOrEmpty(nextUrl))
            {
                var response = await client.GetAsync(nextUrl);
                if (!response.IsSuccessStatusCode) break;

                var result = JsonSerializer.Deserialize<WaniKaniCollectionResponse>(await response.Content.ReadAsStringAsync());
                if (result?.Data == null) break;

                foreach (var item in result.Data)
                {
                    if (string.IsNullOrEmpty(item.Data?.Characters)) continue;
                    string realUnicode = GetUnicodeHex(item.Data.Characters);

                    // ================= RADICAL =================
                    if (item.ObjectType == "radical")
                    {
                        if (!await _context.Radicals.AnyAsync(r => r.WanikaniId == item.Id))
                        {
                            var novoRadical = new Radical
                            {
                                WanikaniId = item.Id,
                                Literal = item.Data.Characters,
                                UnicodeCode = realUnicode,
                                // Campos opcionais (agora nulos se não existirem)
                                KangxiNumber = null,
                                StrokeCount = null,
                                PathImg = null
                            };

                            // NOVO: Adicionar significado na tabela separada RadicalMeaning
                            var meaningText = item.Data.Meanings.FirstOrDefault(m => m.Primary)?.Meaning ?? "Unknown";
                            novoRadical.RadicalMeanings.Add(new RadicalMeaning
                            {
                                IdLanguage = idLangEn, // FK da tabela Language
                                Description = meaningText // Nota: Mantive 'Descrition' (sem P) como no teu banco
                            });

                            _context.Radicals.Add(novoRadical);
                            cRad++;
                        }
                    }
                    // ================= KANJI =================
                    else if (item.ObjectType == "kanji")
                    {
                        if (!await _context.Kanjis.AnyAsync(k => k.WanikaniId == item.Id))
                        {
                            var novoKanji = new Kanji
                            {
                                WanikaniId = item.Id,
                                Literal = item.Data.Characters,
                                UnicodeCode = realUnicode,
                                GradeLevel = (short)item.Data.Level,
                                JlptLevel = 5,
                                IsRadical = false
                            };

                            // Leituras
                            foreach (var r in item.Data.Readings)
                            {
                                novoKanji.KanjiReadings.Add(new KanjiReading
                                {
                                    Type = r.Type,
                                    ReadingKana = r.Reading
                                });
                            }

                            // Significados (Com FK de Language)
                            foreach (var m in item.Data.Meanings)
                            {
                                novoKanji.KanjiMeanings.Add(new KanjiMeaning
                                {
                                    //Lang = "en",
                                    IdLanguage = idLangEn, // FK Obrigatória
                                    Gloss = m.Meaning,
                                    IsPrincipal = m.Primary // Atenção: Teu banco usa 'IsPrincipal'
                                });
                            }

                            _context.Kanjis.Add(novoKanji);
                            await _context.SaveChangesAsync(); // Gera ID

                            if (item.Data.ComponentSubjectIds.Any())
                                pendencias.Add(new PendenciaDecomposicao { LocalIdCriado = novoKanji.Id, Tipo = "kanji", IdsComponentesWk = item.Data.ComponentSubjectIds });

                            cKan++;
                        }
                    }
                    // ================= VOCABULÁRIO =================
                    else if (item.ObjectType == "vocabulary")
                    {
                        if (!await _context.Vocabularies.AnyAsync(v => v.WanikaniId == item.Id))
                        {
                            var novoVocab = new Vocabulary
                            {
                                WanikaniId = item.Id,
                                Characters = item.Data.Characters,
                                Level = (short)item.Data.Level
                            };

                            // Significados
                            foreach (var m in item.Data.Meanings)
                            {
                                novoVocab.VocabularyMeanings.Add(new VocabularyMeaning { Meaning = m.Meaning, IsPrimary = m.Primary });
                            }
                            // Leituras
                            foreach (var r in item.Data.Readings)
                            {
                                novoVocab.VocabularyReadings.Add(new VocabularyReading { Reading = r.Reading, IsPrimary = false });
                            }
                            // Frases
                            foreach (var s in item.Data.ContextSentences)
                            {
                                novoVocab.VocabularyContextSentences.Add(new VocabularyContextSentence { En = s.En, Ja = s.Ja });
                            }

                            _context.Vocabularies.Add(novoVocab);
                            await _context.SaveChangesAsync(); // Gera ID

                            if (item.Data.ComponentSubjectIds.Any())
                                pendencias.Add(new PendenciaDecomposicao { LocalIdCriado = novoVocab.Id, Tipo = "vocabulary", IdsComponentesWk = item.Data.ComponentSubjectIds });

                            cVoc++;
                        }
                    }
                }
                // Salva o lote
                await _context.SaveChangesAsync();
                nextUrl = result.Pages?.NextUrl;
            }

            // --- FASE 2: RELACIONAMENTOS ---
            var mapaRadicais = await _context.Radicals.Where(r => r.WanikaniId != null).ToDictionaryAsync(r => r.WanikaniId!.Value, r => r.Id);
            var mapaKanjis = await _context.Kanjis.Where(k => k.WanikaniId != null).ToDictionaryAsync(k => k.WanikaniId!.Value, k => k.Id);

            int ligacoes = 0;

            foreach (var p in pendencias)
            {
                // KANJI DECOMPOSITION (Igual)
                if (p.Tipo == "kanji")
                {
                    short ordem = 1;
                    foreach (var wkId in p.IdsComponentesWk)
                    {
                        if (mapaRadicais.TryGetValue(wkId, out int radId))
                        {
                            _context.KanjiDecompositions.Add(new KanjiDecomposition { ParentKanjiId = p.LocalIdCriado, ComponentType = "radical", ComponentRadicalId = radId, OrderIndex = ordem++ });
                        }
                        else if (mapaKanjis.TryGetValue(wkId, out int kanId))
                        {
                            _context.KanjiDecompositions.Add(new KanjiDecomposition { ParentKanjiId = p.LocalIdCriado, ComponentType = "kanji", ComponentKanjiId = kanId, OrderIndex = ordem++ });
                        }
                    }
                }
                // VOCABULARY COMPOSITION (Usa SQL Interpolado)
                else if (p.Tipo == "vocabulary")
                {
                    foreach (var wkId in p.IdsComponentesWk)
                    {
                        if (mapaKanjis.TryGetValue(wkId, out int kanjiId))
                        {
                            await _context.Database.ExecuteSqlInterpolatedAsync(
                                $"INSERT INTO vocabulary_composition (vocabulary_id, kanji_id) VALUES ({p.LocalIdCriado}, {kanjiId})"
                            );
                            ligacoes++;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            return $"Sucesso Absoluto! Radicais: {cRad}, Kanjis: {cKan}, Vocabulários: {cVoc}. Ligações: {ligacoes}.";
        }

        public class WkSubjectResponse
        {
            public int id { get; set; }
            public string @object { get; set; }
            public WkKanjiData data { get; set; }
        }

        public class WkKanjiData
        {
            public string characters { get; set; }
            // Este é o campo mágico que queremos!
            public List<int> visually_similar_subject_ids { get; set; } = new List<int>();
        }
        // Adicione este método dentro da classe WaniKaniService

        public async Task<WkSubjectResponse?> GetSubjectById(int wanikaniId)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiToken);
            client.DefaultRequestHeaders.Add("Wanikani-Revision", "20170710");

            // Chama o endpoint para pegar dados de um item específico
            var response = await client.GetAsync($"https://api.wanikani.com/v2/subjects/{wanikaniId}");

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();

            // Configura para ignorar maiúsculas/minúsculas (garantia de segurança)
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            return JsonSerializer.Deserialize<WkSubjectResponse>(json, options);
        }

    }
}