using Denkiishi_v2.Enums;
using Denkiishi_v2.Models;
using Denkiishi_v2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Denkiishi_v2.Controllers
{
    public class QuizController : Controller
    {
        private readonly InasDbContext _context;
        private readonly ISrsService _srsService;

        public QuizController(InasDbContext context, ISrsService srsService)
        {
            _context = context;
            _srsService = srsService;
        }

        /// <summary>
        /// GET: Fallback para evitar erro 405 e permitir testes manuais via URL.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Start(int sessionId, string[] selectedItems)
        {
            if (selectedItems == null || selectedItems.Length == 0)
            {
                TempData["QuizMessage"] = "Selecione itens na lição para iniciar o quiz.";
                return RedirectToAction("Index", "Lesson");
            }
            return await ProcessQuizStart(sessionId, selectedItems);
        }

        /// <summary>
        /// POST: Chamado ao finalizar a lição no Study.cshtml.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int sessionId, [FromForm] string[] selectedItems, string mode = "lesson")
        {
            if (selectedItems == null || selectedItems.Length == 0)
            {
                TempData["QuizMessage"] = "Nenhum item recebido para o quiz.";
                return RedirectToAction("Index", "Lesson");
            }
            return await ProcessQuizStart(sessionId, selectedItems);
        }

        /// <summary>
        /// Lógica centralizada para montagem do Quiz e inicialização da sessão.
        /// </summary>
        private async Task<IActionResult> ProcessQuizStart(int sessionId, string[] selectedItems)
        {
            var langPtBr = await _context.Language.AsNoTracking().FirstOrDefaultAsync(l => l.LanguageCode == "pt-br")
                           ?? await _context.Language.AsNoTracking().FirstOrDefaultAsync();
            int langId = langPtBr?.Id ?? 1;

            var questions = new List<QuizQuestionViewModel>();

            foreach (var itemKey in selectedItems)
            {
                var parts = itemKey.Split('_');
                if (parts.Length != 2 || !int.TryParse(parts[1], out int itemId)) continue;
                string tipo = parts[0].ToLowerInvariant();

                if (tipo == "radical") await AddRadicalQuestionsAsync(itemId, langId, questions);
                else if (tipo == "kanji") await AddKanjiQuestionsAsync(itemId, langId, questions);
                else if (tipo == "vocab" || tipo == "vocabulary") await AddVocabQuestionsAsync(itemId, langId, questions);
            }

            if (questions.Count == 0)
            {
                TempData["QuizMessage"] = "Não foi possível montar perguntas para os itens selecionados.";
                return RedirectToAction("Index", "Lesson");
            }

            // Inicializar/Resetar Sessão no Banco (Resiliência AWS)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
            {
                var oldSessions = _context.QuizSessions.Where(s => s.UserId == userId);
                _context.QuizSessions.RemoveRange(oldSessions);

                _context.QuizSessions.Add(new QuizSession
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CurrentState = "{}",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            var model = new QuizSessionViewModel { SessionId = sessionId, Mode = "lesson", Questions = questions };
            return View("Start", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessResponse(int itemId, string itemType, string questionType, bool isCorrect)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var session = await _context.QuizSessions.FirstOrDefaultAsync(s => s.UserId == userId);
            if (session == null) return BadRequest("Sessão não encontrada.");

            var quizData = JsonSerializer.Deserialize<Dictionary<string, QuizItemState>>(session.CurrentState)
                           ?? new Dictionary<string, QuizItemState>();

            var normalizedItemType = NormalizeItemType(itemType);
            var normalizedQuestionType = NormalizeQuestionType(questionType);

            string key = $"{normalizedItemType}_{itemId}";
            if (!quizData.ContainsKey(key)) quizData[key] = new QuizItemState();
            var state = quizData[key];

            if (isCorrect)
            {
                if (normalizedQuestionType == "meaning") state.MeaningCorrect = true;
                if (normalizedQuestionType == "reading") state.ReadingCorrect = true;
            }
            else
            {
                if (normalizedQuestionType == "meaning") state.MeaningErrors++;
                if (normalizedQuestionType == "reading") state.ReadingErrors++;
            }

            bool isFinished = false;
            if (normalizedItemType == "radical" && state.MeaningCorrect) isFinished = true;

            // Vocab pode ter somente meaning (kana-only). Neste caso, reading não será perguntado e o item deve finalizar com meaning.
            if (normalizedItemType == "vocab")
            {
                var hasKanji = await _context.VocabularyCompositions
                    .AsNoTracking()
                    .AnyAsync(vc => vc.VocabularyId == itemId);

                isFinished = hasKanji ? (state.MeaningCorrect && state.ReadingCorrect) : state.MeaningCorrect;
            }

            if (normalizedItemType == "kanji" && state.MeaningCorrect && state.ReadingCorrect) isFinished = true;

            if (isFinished)
            {
                await PromoteToSrs(userId, itemId, normalizedItemType, state);
            }

            session.CurrentState = JsonSerializer.Serialize(quizData);
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, finishedItem = isFinished });
        }

        private async Task PromoteToSrs(string userId, int itemId, string itemType, QuizItemState state)
        {
            // Padronização: gravamos apenas radical | kanji | vocab (conforme plano)
            string dbType = NormalizeItemType(itemType);

            var progress = await _context.UserProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ItemType == dbType && p.ItemId == itemId);

            int startingStage = progress?.SrsStage ?? 0;
            SrsStage currentStage = (SrsStage)startingStage;

            if (progress == null)
            {
                var (newStage, nextReview, newEase) = _srsService.CalculateNextReview(SrsStage.Initiate, state.MeaningErrors, state.ReadingErrors, 2.50m);

                _context.UserProgresses.Add(new UserProgress
                {
                    UserId = userId,
                    ItemId = itemId,
                    ItemType = dbType,
                    SrsStage = (int)newStage,
                    NextReviewAt = nextReview,
                    EaseFactor = newEase,
                    Interval = 0,
                    ReviewCount = 0,
                    ConsecutiveCorrectCount = 0,
                    LastReviewedAt = DateTime.UtcNow,
                    UnlockedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                progress = await _context.UserProgresses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.ItemType == dbType && p.ItemId == itemId);
            }
            else
            {
                var (newStage, nextReview, newEase) = _srsService.CalculateNextReview(currentStage, state.MeaningErrors, state.ReadingErrors, progress.EaseFactor);
                progress.SrsStage = (int)newStage;
                progress.NextReviewAt = nextReview;
                progress.EaseFactor = newEase;
                progress.UpdatedAt = DateTime.UtcNow;
                progress.LastReviewedAt = DateTime.UtcNow;
            }

            // Recarrega o stage final (insert/update)
            int endingStage = progress?.SrsStage ?? (startingStage == 0 ? 1 : startingStage);

            _context.ReviewHistories.Add(new ReviewHistory
            {
                UserId = userId,
                ItemId = itemId,
                ItemType = dbType,
                MeaningIncorrectCount = state.MeaningErrors,
                ReadingIncorrectCount = state.ReadingErrors,
                StartingSrsStage = startingStage,
                EndingSrsStage = endingStage,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static string NormalizeItemType(string? itemType)
        {
            var t = (itemType ?? string.Empty).Trim().ToLowerInvariant();
            return t switch
            {
                "radical" => "radical",
                "kanji" => "kanji",
                "vocab" => "vocab",
                "vocabulary" => "vocab",
                _ => t
            };
        }

        private static string NormalizeQuestionType(string? questionType)
        {
            var t = (questionType ?? string.Empty).Trim().ToLowerInvariant();
            return t switch
            {
                "meaning" => "meaning",
                "reading" => "reading",
                _ => t
            };
        }

        // --- MÉTODOS DE BUSCA LINGUÍSTICA (MANTIDOS E AJUSTADOS AO DBSET) ---

        private async Task AddRadicalQuestionsAsync(int radicalId, int langId, List<QuizQuestionViewModel> questions)
        {
            var radical = await _context.Radicals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == radicalId);
            if (radical == null) return;
            var meaning = await _context.RadicalMeanings.AsNoTracking().FirstOrDefaultAsync(rm => rm.IdRadical == radicalId && rm.IdLanguage == langId);
            if (meaning == null || string.IsNullOrWhiteSpace(meaning.Description)) return;

            questions.Add(new QuizQuestionViewModel { ItemType = "radical", ItemId = radicalId, Character = radical.Literal, PromptType = "meaning", PromptText = "Qual o significado deste radical?", HelperText = "Responda em Português.", CorrectAnswer = meaning.Description.Trim() });
        }

        private async Task AddKanjiQuestionsAsync(int kanjiId, int langId, List<QuizQuestionViewModel> questions)
        {
            var kanji = await _context.Kanjis.AsNoTracking().FirstOrDefaultAsync(k => k.Id == kanjiId && k.IsActive != false);
            if (kanji == null) return;
            var meaning = await _context.KanjiMeanings.AsNoTracking().Where(km => km.KanjiId == kanjiId && km.IdLanguage == langId).OrderByDescending(km => km.IsPrincipal).FirstOrDefaultAsync();
            if (meaning != null && !string.IsNullOrWhiteSpace(meaning.Gloss))
                questions.Add(new QuizQuestionViewModel { ItemType = "kanji", ItemId = kanjiId, Character = kanji.Literal, PromptType = "meaning", PromptText = "Qual o significado deste kanji?", HelperText = "Responda em Português.", CorrectAnswer = meaning.Gloss.Trim() });

            var reading = await _context.KanjiReadings.AsNoTracking().Where(kr => kr.KanjiId == kanjiId && EF.Functions.ILike(kr.Type, "onyomi")).OrderByDescending(kr => kr.IsPrincipal).FirstOrDefaultAsync();
            if (reading != null && !string.IsNullOrWhiteSpace(reading.ReadingKana))
                questions.Add(new QuizQuestionViewModel { ItemType = "kanji", ItemId = kanjiId, Character = kanji.Literal, PromptType = "reading", PromptText = "Qual a leitura principal (onyomi)?", HelperText = "Digite em hiragana.", CorrectAnswer = reading.ReadingKana.Trim() });
        }

        private async Task AddVocabQuestionsAsync(int vocabId, int langId, List<QuizQuestionViewModel> questions)
        {
            var vocab = await _context.Vocabularies.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vocabId && v.IsActive != false);
            if (vocab == null) return;
            var meaning = await _context.VocabularyMeanings.AsNoTracking().Where(vm => vm.VocabularyId == vocabId && vm.LanguageId == langId).OrderByDescending(vm => vm.IsPrimary).FirstOrDefaultAsync();
            if (meaning != null && !string.IsNullOrWhiteSpace(meaning.Meaning))
                questions.Add(new QuizQuestionViewModel { ItemType = "vocab", ItemId = vocabId, Character = vocab.Characters, PromptType = "meaning", PromptText = "Qual o significado deste vocabulário?", HelperText = "Responda em Português.", CorrectAnswer = meaning.Meaning.Trim() });

            bool hasKanji = await _context.VocabularyCompositions.AsNoTracking().AnyAsync(vc => vc.VocabularyId == vocabId);
            if (!hasKanji) return;

            var reading = await _context.VocabularyReadings.AsNoTracking().Where(vr => vr.VocabularyId == vocabId).OrderByDescending(vr => vr.IsPrimary).FirstOrDefaultAsync();
            if (reading != null && !string.IsNullOrWhiteSpace(reading.Reading))
                questions.Add(new QuizQuestionViewModel { ItemType = "vocab", ItemId = vocabId, Character = vocab.Characters, PromptType = "reading", PromptText = "Qual a leitura principal?", HelperText = "Digite em hiragana/katakana.", CorrectAnswer = reading.Reading.Trim() });
        }
    }
}