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

        private const string ApiToken = "0f985f01-f9c2-4ea9-b05f-767c019a54be";

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

        private async Task<int> GetOrCreateLanguageId(string code, string name)
        {
            var lang = await _context.Language.FirstOrDefaultAsync(l => l.LanguageCode == code);
            if (lang != null) return lang.Id;

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

            string? nextUrl = "https://api.wanikani.com/v2/subjects?types=radical,kanji,vocabulary";
            var pendencias = new List<PendenciaDecomposicao>();
            int cRad = 0, cKan = 0, cVoc = 0;

            int idLangEn = await GetOrCreateLanguageId("en-US", "English (US)");

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

                    if (item.ObjectType == "radical")
                    {
                        if (!await _context.Radicals.AnyAsync(r => r.WanikaniId == item.Id))
                        {
                            var novoRadical = new Radical { WanikaniId = item.Id, Literal = item.Data.Characters, UnicodeCode = realUnicode };
                            var meaningText = item.Data.Meanings.FirstOrDefault(m => m.Primary)?.Meaning ?? "Unknown";
                            novoRadical.RadicalMeanings.Add(new RadicalMeaning { IdLanguage = idLangEn, Description = meaningText });
                            _context.Radicals.Add(novoRadical);
                            cRad++;
                        }
                    }
                    else if (item.ObjectType == "kanji")
                    {
                        if (!await _context.Kanjis.AnyAsync(k => k.WanikaniId == item.Id))
                        {
                            var novoKanji = new Kanji { WanikaniId = item.Id, Literal = item.Data.Characters, UnicodeCode = realUnicode, GradeLevel = (short)item.Data.Level, JlptLevel = 5, IsRadical = false };
                            foreach (var r in item.Data.Readings) { novoKanji.KanjiReadings.Add(new KanjiReading { Type = r.Type, ReadingKana = r.Reading }); }
                            foreach (var m in item.Data.Meanings) { novoKanji.KanjiMeanings.Add(new KanjiMeaning { IdLanguage = idLangEn, Gloss = m.Meaning, IsPrincipal = m.Primary }); }
                            _context.Kanjis.Add(novoKanji);
                            await _context.SaveChangesAsync();
                            if (item.Data.ComponentSubjectIds.Any()) pendencias.Add(new PendenciaDecomposicao { LocalIdCriado = novoKanji.Id, Tipo = "kanji", IdsComponentesWk = item.Data.ComponentSubjectIds });
                            cKan++;
                        }
                    }
                    else if (item.ObjectType == "vocabulary")
                    {
                        if (!await _context.Vocabularies.AnyAsync(v => v.WanikaniId == item.Id))
                        {
                            var novoVocab = new Vocabulary { WanikaniId = item.Id, Characters = item.Data.Characters, Level = (short)item.Data.Level };

                            foreach (var m in item.Data.Meanings)
                            {
                                novoVocab.VocabularyMeanings.Add(new VocabularyMeaning { Meaning = m.Meaning, LanguageId = idLangEn });
                            }

                            foreach (var r in item.Data.Readings)
                            {
                                // VALIDAÇÃO: No seu DTO 'WaniKaniReading', a propriedade chama-se 'IsPrimary'
                                novoVocab.VocabularyReadings.Add(new VocabularyReading { Reading = r.Reading, IsPrimary = r.Primary }); 
                            }

                            foreach (var s in item.Data.ContextSentences)
                            {
                                // VALIDAÇÃO: No seu DTO é 'Ja' e 'En'. No seu Model é 'Jp' e 'En'.
                                novoVocab.VocabularyContextSentences.Add(new VocabularyContextSentence { En = s.En, Jp = s.Ja, LanguageId = idLangEn });
                            }

                            _context.Vocabularies.Add(novoVocab);
                            await _context.SaveChangesAsync();
                            if (item.Data.ComponentSubjectIds.Any()) pendencias.Add(new PendenciaDecomposicao { LocalIdCriado = novoVocab.Id, Tipo = "vocabulary", IdsComponentesWk = item.Data.ComponentSubjectIds });
                            cVoc++;
                        }
                    }
                }
                await _context.SaveChangesAsync();
                nextUrl = result.Pages?.NextUrl;
            }

            var mapaRadicais = await _context.Radicals.Where(r => r.WanikaniId != null).ToDictionaryAsync(r => r.WanikaniId!.Value, r => r.Id);
            var mapaKanjis = await _context.Kanjis.Where(k => k.WanikaniId != null).ToDictionaryAsync(k => k.WanikaniId!.Value, k => k.Id);

            int ligacoes = 0;
            foreach (var p in pendencias)
            {
                if (p.Tipo == "kanji")
                {
                    short ordem = 1;
                    foreach (var wkId in p.IdsComponentesWk)
                    {
                        if (mapaRadicais.TryGetValue(wkId, out int radId))
                            _context.KanjiDecompositions.Add(new KanjiDecomposition { ParentKanjiId = p.LocalIdCriado, ComponentType = "radical", ComponentRadicalId = radId, OrderIndex = ordem++ });
                        else if (mapaKanjis.TryGetValue(wkId, out int kanId))
                            _context.KanjiDecompositions.Add(new KanjiDecomposition { ParentKanjiId = p.LocalIdCriado, ComponentType = "kanji", ComponentKanjiId = kanId, OrderIndex = ordem++ });
                    }
                }
                else if (p.Tipo == "vocabulary")
                {
                    foreach (var wkId in p.IdsComponentesWk)
                    {
                        if (mapaKanjis.TryGetValue(wkId, out int kanjiId))
                        {
                            _context.VocabularyCompositions.Add(new VocabularyComposition { VocabularyId = p.LocalIdCriado, KanjiId = kanjiId });
                            ligacoes++;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            return $"Importação OK! Radicais: {cRad}, Kanjis: {cKan}, Vocab: {cVoc}. Ligações: {ligacoes}.";
        }

        public async Task<WkSubjectResponse?> GetSubjectById(int wanikaniId)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiToken);
            client.DefaultRequestHeaders.Add("Wanikani-Revision", "20170710");
            var response = await client.GetAsync($"https://api.wanikani.com/v2/subjects/{wanikaniId}");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<WkSubjectResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public class WkSubjectResponse { public int id { get; set; } public string @object { get; set; } public WkKanjiData data { get; set; } }
        public class WkKanjiData { public string characters { get; set; } public List<int> visually_similar_subject_ids { get; set; } = new(); }
    }
}