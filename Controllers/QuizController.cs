using Denkiishi_v2.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Denkiishi_v2.Controllers
{
    public class QuizController : Controller
    {
        private readonly InasDbContext _context;

        public QuizController(InasDbContext context)
        {
            _context = context;
        }

        /// <summary>GET: /Quiz/Start — Sem itens; redireciona para a lição.</summary>
        [HttpGet]
        public IActionResult Start(int? sessionId)
        {
            TempData["QuizMessage"] = "Selecione itens na lição e conclua o estudo para iniciar o quiz.";
            return RedirectToAction("Index", "Lesson");
        }

        /// <summary>POST: /Quiz/Start — Recebe itens da lição e monta perguntas a partir do banco (radical: meaning; kanji: meaning + reading onyomi; vocab: meaning + reading se tiver kanji).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int sessionId, string[] selectedItems)
        {
            if (selectedItems == null || selectedItems.Length == 0)
            {
                TempData["QuizMessage"] = "Nenhum item selecionado. Volte à lição e escolha os itens.";
                return RedirectToAction("Index", "Lesson");
            }

            var langPtBr = await _context.Language
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LanguageCode == "pt-br")
                ?? await _context.Language.AsNoTracking().FirstOrDefaultAsync();
            int langId = langPtBr?.Id ?? 1;

            var questions = new List<QuizQuestionViewModel>();

            foreach (var itemKey in selectedItems)
            {
                var parts = itemKey.Split('_');
                if (parts.Length != 2 || !int.TryParse(parts[1], out int itemId)) continue;

                string tipo = parts[0].ToLowerInvariant();

                if (tipo == "radical")
                    await AddRadicalQuestionsAsync(itemId, langId, questions);
                else if (tipo == "kanji")
                    await AddKanjiQuestionsAsync(itemId, langId, questions);
                else if (tipo == "vocab")
                    await AddVocabQuestionsAsync(itemId, langId, questions);
            }

            if (questions.Count == 0)
            {
                TempData["QuizMessage"] = "Não foi possível montar perguntas para os itens selecionados. Verifique se há significados/leituras em Português (pt-BR).";
                return RedirectToAction("Index", "Lesson");
            }

            var model = new QuizSessionViewModel
            {
                SessionId = sessionId,
                Mode = "lesson",
                Questions = questions
            };

            return View("Start", model);
        }

        private async Task AddRadicalQuestionsAsync(int radicalId, int langId, List<QuizQuestionViewModel> questions)
        {
            var radical = await _context.Radicals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == radicalId);
            if (radical == null) return;

            var meaning = await _context.Set<RadicalMeaning>()
                .AsNoTracking()
                .FirstOrDefaultAsync(rm => rm.IdRadical == radicalId && rm.IdLanguage == langId);
            if (meaning == null || string.IsNullOrWhiteSpace(meaning.Description)) return;

            questions.Add(new QuizQuestionViewModel
            {
                ItemType = "radical",
                ItemId = radicalId,
                Character = radical.Literal,
                PromptType = "meaning",
                PromptText = "Qual é o significado deste radical?",
                HelperText = "Responda em Português.",
                CorrectAnswer = meaning.Description.Trim()
            });
        }

        private async Task AddKanjiQuestionsAsync(int kanjiId, int langId, List<QuizQuestionViewModel> questions)
        {
            var kanji = await _context.Kanjis.AsNoTracking().FirstOrDefaultAsync(k => k.Id == kanjiId && k.IsActive != false);
            if (kanji == null) return;

            // Significado principal (pt-BR): is_principal = true primeiro
            var meaning = await _context.Set<KanjiMeaning>()
                .AsNoTracking()
                .Where(km => km.KanjiId == kanjiId && km.IdLanguage == langId)
                .OrderByDescending(km => km.IsPrincipal)
                .FirstOrDefaultAsync();
            if (meaning != null && !string.IsNullOrWhiteSpace(meaning.Gloss))
            {
                questions.Add(new QuizQuestionViewModel
                {
                    ItemType = "kanji",
                    ItemId = kanjiId,
                    Character = kanji.Literal,
                    PromptType = "meaning",
                    PromptText = "Qual é o significado deste kanji?",
                    HelperText = "Responda em Português.",
                    CorrectAnswer = meaning.Gloss.Trim(),
                    PrincipalHint = "Estamos considerando o significado principal."
                });
            }

            // Leitura principal em onyomi: is_principal = true primeiro (EF não traduz StringComparison; ILike gera SQL ILIKE no PostgreSQL)
            var reading = await _context.Set<KanjiReading>()
                .AsNoTracking()
                .Where(kr => kr.KanjiId == kanjiId && EF.Functions.ILike(kr.Type, "onyomi"))
                .OrderByDescending(kr => kr.IsPrincipal)
                .FirstOrDefaultAsync();
            if (reading != null && !string.IsNullOrWhiteSpace(reading.ReadingKana))
            {
                questions.Add(new QuizQuestionViewModel
                {
                    ItemType = "kanji",
                    ItemId = kanjiId,
                    Character = kanji.Literal,
                    PromptType = "reading",
                    PromptText = "Qual é a leitura principal deste kanji?",
                    HelperText = "Digite a leitura em hiragana (onyomi).",
                    CorrectAnswer = reading.ReadingKana.Trim(),
                    PrincipalHint = "Estamos considerando a leitura principal em onyomi."
                });
            }
        }

        private async Task AddVocabQuestionsAsync(int vocabId, int langId, List<QuizQuestionViewModel> questions)
        {
            var vocab = await _context.Vocabularies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vocabId && v.IsActive != false);
            if (vocab == null) return;

            // Significado principal (pt-BR): is_primary primeiro
            var meaning = await _context.Set<VocabularyMeaning>()
                .AsNoTracking()
                .Where(vm => vm.VocabularyId == vocabId && vm.LanguageId == langId)
                .OrderByDescending(vm => vm.IsPrimary)
                .FirstOrDefaultAsync();
            if (meaning != null && !string.IsNullOrWhiteSpace(meaning.Meaning))
            {
                questions.Add(new QuizQuestionViewModel
                {
                    ItemType = "vocab",
                    ItemId = vocabId,
                    Character = vocab.Characters,
                    PromptType = "meaning",
                    PromptText = "Qual é o significado deste vocabulário?",
                    HelperText = "Responda em Português.",
                    CorrectAnswer = meaning.Meaning.Trim()
                });
            }

            // Leitura: só se o vocabulário tiver kanji na composição
            bool hasKanji = await _context.Set<VocabularyComposition>()
                .AsNoTracking()
                .AnyAsync(vc => vc.VocabularyId == vocabId);
            if (!hasKanji) return;

            var reading = await _context.Set<VocabularyReading>()
                .AsNoTracking()
                .Where(vr => vr.VocabularyId == vocabId)
                .OrderByDescending(vr => vr.IsPrimary)
                .FirstOrDefaultAsync();
            if (reading != null && !string.IsNullOrWhiteSpace(reading.Reading))
            {
                questions.Add(new QuizQuestionViewModel
                {
                    ItemType = "vocab",
                    ItemId = vocabId,
                    Character = vocab.Characters,
                    PromptType = "reading",
                    PromptText = "Qual é a leitura principal deste vocabulário?",
                    HelperText = "Digite a leitura em hiragana/katakana.",
                    CorrectAnswer = reading.Reading.Trim(),
                    PrincipalHint = "Estamos considerando a leitura principal."
                });
            }
        }
    }
}
