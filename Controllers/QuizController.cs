using Denkiishi_v2.Models;
using Denkiishi_v2.Services;
using Denkiishi_v2.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using QuizSession; 
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

        // ... (Método Start mantido conforme anterior) ...

        [HttpPost]
        public async Task<IActionResult> ProcessResponse(int itemId, string itemType, string questionType, bool isCorrect)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            // Busca sessão ativa
            var session = await _context.QuizSessions.FirstOrDefaultAsync(s => s.UserId == userId);
            if (session == null) return BadRequest("Sessão expirada.");

            var quizData = JsonSerializer.Deserialize<Dictionary<string, QuizItemState>>(session.CurrentState)
                           ?? new Dictionary<string, QuizItemState>();

            string key = $"{itemType}_{itemId}";
            if (!quizData.ContainsKey(key)) quizData[key] = new QuizItemState();
            var state = quizData[key];

            // Atualiza erros/acertos
            if (isCorrect)
            {
                if (questionType == "meaning") state.MeaningCorrect = true;
                if (questionType == "reading") state.ReadingCorrect = true;
            }
            else
            {
                if (questionType == "meaning") state.MeaningErrors++;
                if (questionType == "reading") state.ReadingErrors++;
            }

            // Verifica conclusão do item
            bool isFinished = false;
            if (itemType == "radical" && state.MeaningCorrect) isFinished = true;
            if ((itemType == "kanji" || itemType == "vocab") && state.MeaningCorrect && state.ReadingCorrect) isFinished = true;

            if (isFinished)
            {
                await PromoteToSrs(userId, itemId, itemType, state);
            }

            session.CurrentState = JsonSerializer.Serialize(quizData);
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, finishedItem = isFinished });
        }

        private async Task PromoteToSrs(string userId, int itemId, string itemType, QuizItemState state)
        {
            // Ajustando para os nomes de tabela do seu InasDbContext
            var progress = await _context.UserProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ItemType == itemType && p.ItemId == itemId);

            int totalErrors = state.MeaningErrors + state.ReadingErrors;

            if (progress == null)
            {
                // Primeiro acerto (Nascimento no SRS)
                var (newStage, nextReview, newEase) = _srsService.CalculateNextReview(SrsStage.Initiate, state.MeaningErrors, state.ReadingErrors, 2.50m);

                _context.UserProgresses.Add(new UserProgress
                {
                    UserId = userId,
                    ItemId = itemId,
                    ItemType = itemType,
                    SrsStage = (int)newStage,
                    NextReviewAt = nextReview,
                    EaseFactor = newEase,
                    UnlockedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                // Revisão (Evolução)
                var (newStage, nextReview, newEase) = _srsService.CalculateNextReview((SrsStage)progress.SrsStage, state.MeaningErrors, state.ReadingErrors, progress.EaseFactor);

                progress.SrsStage = (int)newStage;
                progress.NextReviewAt = nextReview;
                progress.EaseFactor = newEase;
                progress.UpdatedAt = DateTime.UtcNow;
            }

            // Log no histórico usando ReviewHistories (DbSet do seu Contexto)
            _context.ReviewHistories.Add(new ReviewHistory
            {
                UserId = userId,
                ItemId = itemId,
                ItemType = itemType,
                MeaningIncorrectCount = state.MeaningErrors,
                ReadingIncorrectCount = state.ReadingErrors,
                StartingSrsStage = progress?.SrsStage ?? 0,
                EndingSrsStage = progress?.SrsStage ?? 1,
                CreatedAt = DateTime.UtcNow
            });
        }

        // ... (Seus métodos privados de busca de perguntas AddKanjiQuestionsAsync etc aqui) ...
    }
}