using Microsoft.AspNetCore.Mvc;
using Denkiishi_v2.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace Denkiishi_v2.Controllers
{
    // [Authorize]
    public class LessonController : Controller
    {
        private readonly InasDbContext _context;

        // Injetando o banco de dados
        public LessonController(InasDbContext context)
        {
            _context = context;
        }

        // GET: /Lesson/Index (Seu dashboard de Seleção continua aqui)
        public IActionResult Index()
        {
            // (MANTENHA O CÓDIGO DO MOCK DO DASHBOARD QUE FIZEMOS ANTES AQUI)
            // ...
            var dashboard = new LessonDashboardViewModel();
            var mandala1 = new MandalaStudentDto { Id = 1, Nome = "Água", Ordem = 1 };
            var circulo1 = new CircleStudentDto { Id = 1, Nome = "Gotas Iniciais", Ordem = 1 };

            // Vamos testar o radical 36 (Arco) que você mandou na query!
            circulo1.Itens.Add(new ItemStudentDto { IdOriginal = 9, Tipo = "radical", Caractere = "人" });
            circulo1.Itens.Add(new ItemStudentDto { IdOriginal = 705, Tipo = "kanji", Caractere = "審" });
            circulo1.Itens.Add(new ItemStudentDto { IdOriginal = 15061, Tipo = "vocab", Caractere = "三日" });

            mandala1.Circulos.Add(circulo1);
            dashboard.Mandalas.Add(mandala1);

            return View(dashboard);
        }

        // POST: /Lesson/StartSession
        [HttpPost]
        public async Task<IActionResult> StartSession(string[] selectedItems)
        {
            if (selectedItems == null || !selectedItems.Any()) return RedirectToAction("Index");

            var session = new LessonSessionViewModel { SessionId = 1 };

            // Pega o ID da língua PT-BR
            var linguaPtBr = await _context.Language.FirstOrDefaultAsync(l => l.LanguageCode == "pt-br")
                          ?? await _context.Language.FirstOrDefaultAsync();
            int langId = linguaPtBr?.Id ?? 1;

            // BUSCA AS REGRAS DE CORES UMA ÚNICA VEZ
            var regrasSyntax = await _context.Set<SyntaxHighlight>().ToListAsync();

            foreach (var itemKey in selectedItems)
            {
                var parts = itemKey.Split('_'); // Ex: "radical_36"
                if (parts.Length != 2) continue;

                string tipo = parts[0];
                int id = int.Parse(parts[1]);

                var lessonItem = new LessonItemViewModel { Id = id, Tipo = tipo };

                if (tipo == "radical")
                {
                    // 1. Busca o Radical (select * from radical where id = X)
                    var radical = await _context.Radicals.FindAsync(id);
                    if (radical == null) continue;

                    lessonItem.Caractere = radical.Literal;

                    // 2. Busca o Meaning em PT-BR
                    var meaning = await _context.Set<RadicalMeaning>()
                        .FirstOrDefaultAsync(m => m.IdRadical == id && m.IdLanguage == langId);

                    if (meaning != null)
                    {
                        lessonItem.SignificadoPrincipal = meaning.Description;

                        // 3. Busca a História (Mnemonic)
                        var mnemonic = await _context.Set<RadicalMeaningMnemonic>()
                            .FirstOrDefaultAsync(m => m.RadicalMeaningId == meaning.Id && m.IsActive == true);

                        string textoBruto = mnemonic?.Text ?? "Ainda não há história para este radical.";

                        // APLICA A MAGIA DA FORMATAÇÃO AQUI
                        lessonItem.MnemonicSignificado = FormatarMnemonic(textoBruto, regrasSyntax);
                    }
                    else
                    {
                        lessonItem.SignificadoPrincipal = "Sem tradução";
                        lessonItem.MnemonicSignificado = "Ainda não há história para este radical.";
                    }

                    // 4. Busca Max 3 Kanjis que usam este radical
                    lessonItem.KanjisRelacionados = await (from kr in _context.Set<KanjiRadical>()
                                                           where kr.RadicalId == id
                                                           join k in _context.Kanjis on kr.KanjiId equals k.Id

                                                           // JEITO NOVO E SEGURO DE FAZER O LEFT JOIN:
                                                           from km in _context.Set<KanjiMeaning>()
                                                              .Where(m => m.KanjiId == k.Id && m.IdLanguage == langId)
                                                              .DefaultIfEmpty()

                                                           select new RelatedKanjiDto
                                                           {
                                                               Id = k.Id,
                                                               Caractere = k.Literal,
                                                               Significado = km != null ? km.Gloss : "Sem tradução"
                                                           }).Take(3).ToListAsync();
                }
                // (Aqui em baixo, no futuro, colocaremos as regras do Kanji e do Vocab)
                else if (tipo == "kanji")
                {
                    // 1. Busca o Kanji
                    var kanji = await _context.Kanjis.FindAsync(id);
                    if (kanji == null || kanji.IsActive == false) continue; // Só traz se estiver ativo!

                    lessonItem.Caractere = kanji.Literal;

                    // 2. Busca Significados (Português, Max 3, Principal Primeiro)
                    var meanings = await _context.Set<KanjiMeaning>()
                        .Where(m => m.KanjiId == id && m.IdLanguage == langId)
                        .OrderByDescending(m => m.IsPrincipal) // O true vem antes do false
                        .Take(3)
                        .ToListAsync();

                    lessonItem.Significados = meanings.Select(m => new ItemTextDto { Texto = m.Gloss, IsPrimary = m.IsPrincipal ?? false }).ToList();

                    var mainMeaning = meanings.FirstOrDefault(m => m.IsPrincipal == true) ?? meanings.FirstOrDefault();
                    lessonItem.SignificadoPrincipal = mainMeaning?.Gloss ?? "Sem tradução";

                    if (mainMeaning != null)
                    {
                        var mnemSig = await _context.Set<KanjiMeaningMnemonic>()
                            .FirstOrDefaultAsync(m => m.KanjiMeaningId == mainMeaning.Id && m.IsActive == true);
                        lessonItem.MnemonicSignificado = FormatarMnemonic(mnemSig?.Text, regrasSyntax);
                    }

                    // 3. Busca Leituras (Todas, Principal Primeiro)
                    var readings = await _context.Set<KanjiReading>()
                        .Where(r => r.KanjiId == id)
                        .OrderByDescending(r => r.IsPrincipal)
                        .ToListAsync();

                    lessonItem.Leituras = readings.Select(r => new ItemTextDto { Texto = r.ReadingKana, IsPrimary = r.IsPrincipal ?? false }).ToList();

                    var mainReading = readings.FirstOrDefault(r => r.IsPrincipal == true) ?? readings.FirstOrDefault();
                    lessonItem.LeituraPrincipal = mainReading?.ReadingKana ?? "Sem leitura";

                    if (mainReading != null)
                    {
                        var mnemLei = await _context.Set<KanjiReadingMnemonic>()
                            .FirstOrDefaultAsync(m => m.KanjiReadingId == mainReading.Id && m.IsActive == true);
                        lessonItem.MnemonicLeitura = FormatarMnemonic(mnemLei?.Text, regrasSyntax);
                    }

                    // 4. Busca Vocabulários Relacionados (Max 3, apenas Ativos)
                    lessonItem.VocabulariosRelacionados = await (from vc in _context.Set<VocabularyComposition>()
                                                                 join v in _context.Vocabularies on vc.VocabularyId equals v.Id
                                                                 where vc.KanjiId == id && v.IsActive == true
                                                                 select new RelatedVocabDto
                                                                 {
                                                                     Id = v.Id,
                                                                     Caractere = v.Characters
                                                                 }).Take(3).ToListAsync();
                }
                else if (tipo == "vocab")
                {
                    // 1. Busca o Vocabulário (SEMPRE PELO ID!)
                    var vocab = await _context.Vocabularies.FindAsync(id);
                    if (vocab == null || vocab.IsActive == false) continue;

                    lessonItem.Caractere = vocab.Characters;

                    // 1.5. Busca as Classes Gramaticais (Parts of Speech)
                    // A tradução exata do seu SQL com o filtro de idioma
                    lessonItem.ClassesGramaticais = await (from vpsm in _context.Set<VocabularyPartOfSpeechMap>()
                                                           join vps in _context.Set<VocabularyPartOfSpeech>() on vpsm.VocabularyPartOfSpeechId equals vps.Id
                                                           where vpsm.VocabularyId == id && vps.language_id == langId
                                                           select vps.Name).ToListAsync();

                    // 2. Busca Significados (Filtra por Idioma, Traz todos, Principal Primeiro)
                    // Atenção: Usei "LanguageId" conforme o padrão do seu EF Core, se no seu banco estiver "IdLanguage" na classe, basta ajustar.
                    var meanings = await _context.Set<VocabularyMeaning>()
                        .Where(m => m.VocabularyId == id && m.LanguageId == langId)
                        .OrderByDescending(m => m.IsPrimary)
                        .ToListAsync();

                    lessonItem.Significados = meanings.Select(m => new ItemTextDto { Texto = m.Meaning, IsPrimary = m.IsPrimary }).ToList();

                    var mainMeaning = meanings.FirstOrDefault(m => m.IsPrimary == true) ?? meanings.FirstOrDefault();
                    lessonItem.SignificadoPrincipal = mainMeaning?.Meaning ?? "Sem tradução";

                    // 2.1 Busca o Mnemônico do Significado Principal
                    if (mainMeaning != null)
                    {
                        var mnemSig = await _context.Set<VocabularyMeaningMnemonic>()
                            .FirstOrDefaultAsync(m => m.VocabularyMeaningId == mainMeaning.Id && m.IsActive == true);

                        lessonItem.MnemonicSignificado = FormatarMnemonic(mnemSig?.Text, regrasSyntax);
                    }

                    // 3. Busca Leituras (Todas, Principal Primeiro)
                    var readings = await _context.Set<VocabularyReading>()
                        .Where(r => r.VocabularyId == id)
                        .OrderByDescending(r => r.IsPrimary)
                        .ToListAsync();

                    lessonItem.Leituras = readings.Select(r => new ItemTextDto { Texto = r.Reading, IsPrimary = r.IsPrimary ?? false }).ToList();

                    var mainReading = readings.FirstOrDefault(r => r.IsPrimary == true) ?? readings.FirstOrDefault();
                    lessonItem.LeituraPrincipal = mainReading?.Reading ?? "Sem leitura";

                    // 3.1 Busca o Mnemônico da Leitura Principal
                    if (mainReading != null)
                    {
                        var mnemLei = await _context.Set<VocabularyReadingMnemonic>()
                            .FirstOrDefaultAsync(m => m.VocabularyReadingId == mainReading.Id && m.IsActive == true);

                        lessonItem.MnemonicLeitura = FormatarMnemonic(mnemLei?.Text, regrasSyntax);
                    }

                    // 4. Busca os Kanjis que compõem este vocabulário
                    lessonItem.KanjisRelacionados = await (from vc in _context.Set<VocabularyComposition>()
                                                           where vc.VocabularyId == id
                                                           join k in _context.Kanjis on vc.KanjiId equals k.Id

                                                           // ADICIONAMOS O IsPrimary == true AQUI PARA NÃO DUPLICAR!
                                                           from km in _context.Set<KanjiMeaning>()
                                                              .Where(m => m.KanjiId == k.Id && m.IdLanguage == langId && m.IsPrincipal == true)
                                                              .DefaultIfEmpty()

                                                           select new RelatedKanjiDto
                                                           {
                                                               Id = k.Id,
                                                               Caractere = k.Literal,
                                                               Significado = km != null ? km.Gloss : "Sem tradução"
                                                           })
                                                           .Distinct() // Garante que mesmo que o Kanji apareça 2x na palavra, mostre só 1 card
                                                           .ToListAsync();
                }

                session.Itens.Add(lessonItem);
            }

            // Manda os dados diretos para a tela de Study!
            return View("Study", session);
        }
        // ==========================================
        // HELPER PARA FORMATAR MNEMÔNICOS (CORRIGIDO)
        // ==========================================
        private string FormatarMnemonic(string rawText, List<SyntaxHighlight> regras)
        {
            if (string.IsNullOrEmpty(rawText)) return rawText;

            string formattedText = rawText;

            foreach (var regra in regras)
            {
                // 1. Lê as propriedades do banco (Removido o ?? pois os campos não são nulos)
                string bgColor = string.IsNullOrEmpty(regra.BackgroundColor) ? "transparent" : regra.BackgroundColor;
                string textColor = string.IsNullOrEmpty(regra.TextColor) ? "inherit" : regra.TextColor;

                // CORREÇÃO AQUI: Como já são bool direto, usamos direto no ternário
                string fontWeight = regra.IsBold ? "bold" : "normal";
                string fontStyle = regra.IsItalic ? "italic" : "normal";
                string textDecoration = regra.IsUnderline ? "underline" : "none";

                // 2. Monta o CSS completo
                string estiloCss = $@"
                    background-color: {bgColor}; 
                    color: {textColor}; 
                    font-weight: {fontWeight}; 
                    font-style: {fontStyle}; 
                    text-decoration: {textDecoration}; 
                    padding: 0.1em 0.3em; 
                    border-radius: 4px;
                ".Replace("\r\n", "").Replace("\n", "").Replace("  ", "");

                string openTag = $"<span style=\"{estiloCss}\">";
                string closeTag = "</span>";

                // 3. Faz o Replace das tags 
                formattedText = System.Text.RegularExpressions.Regex.Replace(
                    formattedText,
                    $"<{regra.Code}>",
                    openTag,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                formattedText = System.Text.RegularExpressions.Regex.Replace(
                    formattedText,
                    $"</{regra.Code}>",
                    closeTag,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Troca as quebras de linha do banco por <br> do HTML
            return formattedText.Replace("\n", "<br>");
        }
    }
}