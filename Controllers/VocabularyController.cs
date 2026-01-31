using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Denkiishi_v2.Models;

namespace Denkiishi_v2.Controllers
{
    public class VocabularyController : Controller
    {
        private readonly InasDbContext _context;

        public VocabularyController(InasDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Edit(int id)
        {
            var vocabulary = await _context.Vocabularies
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vocabulary == null) return NotFound();

            var viewModel = new VocabularyEditViewModel
            {
                Id = vocabulary.Id,
                Characters = vocabulary.Characters,
                Level = (int)(vocabulary.Level ?? 0),
                AvailableLanguages = await _context.Language.ToListAsync(),

                ExistingMeanings = await _context.VocabularyMeanings
                    .Where(m => m.VocabularyId == id)
                    .Include(m => m.Language)
                    .ToListAsync(),

                ExistingSentences = await _context.VocabularyContextSentences
                    .Where(s => s.VocabularyId == id)
                    .Include(s => s.Language)
                    .ToListAsync(),

                // CORREÇÃO DO JOIN: Agora com VocabularyCompositions mapeado
                KanjiComponents = await (from vc in _context.VocabularyCompositions
                                         join k in _context.Kanjis on vc.KanjiId equals k.Id
                                         where vc.VocabularyId == id
                                         select k).ToListAsync()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AddSentence(int vocabularyId, int languageId, string textJp, string textTranslated)
        {
            var newSentence = new VocabularyContextSentence
            {
                VocabularyId = vocabularyId,
                LanguageId = languageId,
                Jp = textJp,        // Correção: de 'Ja' para 'Jp'
                En = textTranslated // Conforme seu Model
            };

            _context.VocabularyContextSentences.Add(newSentence);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = vocabularyId });
        }

        [HttpPost]
        public async Task<IActionResult> AddMeaning(int vocabularyId, int languageId, string meaning)
        {
            var newMeaning = new VocabularyMeaning
            {
                VocabularyId = vocabularyId,
                LanguageId = languageId,
                Meaning = meaning,
                Type = "primary" // Correção: de 'IsPrimary' para 'Type'
            };

            _context.VocabularyMeanings.Add(newMeaning);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = vocabularyId });
        }
    }
}