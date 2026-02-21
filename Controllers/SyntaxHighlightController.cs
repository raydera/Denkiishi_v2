using Denkiishi_v2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Denkiishi_v2.Controllers
{
    [Authorize(Roles = "Admin")] // Protege o acesso
    public class SyntaxHighlightController : Controller
    {
        private readonly InasDbContext _context;

        public SyntaxHighlightController(InasDbContext context)
        {
            _context = context;
        }

        // GET: SyntaxHighlight
        public async Task<IActionResult> Index()
        {
            return View(await _context.SyntaxHighlights.OrderBy(x => x.Code).ToListAsync());
        }

        // GET: SyntaxHighlight/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: SyntaxHighlight/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SyntaxHighlight syntaxHighlight)
        {
            if (ModelState.IsValid)
            {
                // Verifica duplicidade de código
                if (await _context.SyntaxHighlights.AnyAsync(s => s.Code == syntaxHighlight.Code))
                {
                    ModelState.AddModelError("Code", "Este código já existe.");
                    return View(syntaxHighlight);
                }
                // --- LINHA DE SEGURANÇA ADICIONADA ---
                syntaxHighlight.CreatedAt = DateTime.UtcNow; // Garante UTC antes de salvar
                // -------------------------------------

                _context.Add(syntaxHighlight);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(syntaxHighlight);
        }

        // GET: SyntaxHighlight/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var syntaxHighlight = await _context.SyntaxHighlights.FindAsync(id);
            if (syntaxHighlight == null) return NotFound();
            return View(syntaxHighlight);
        }

        // POST: SyntaxHighlight/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SyntaxHighlight syntaxHighlight)
        {
            if (id != syntaxHighlight.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // --- CORREÇÃO DE UTC PARA O POSTGRESQL ---
                    // O Postgres exige que a data tenha Kind=Utc explicitamente
                    syntaxHighlight.CreatedAt = DateTime.SpecifyKind(syntaxHighlight.CreatedAt, DateTimeKind.Utc);
                    // -----------------------------------------
                    // Mantém a data de criação original se o EF não rastrear
                    _context.Update(syntaxHighlight);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SyntaxHighlightExists(syntaxHighlight.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(syntaxHighlight);
        }

        // POST: SyntaxHighlight/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var syntaxHighlight = await _context.SyntaxHighlights.FindAsync(id);
            if (syntaxHighlight != null)
            {
                _context.SyntaxHighlights.Remove(syntaxHighlight);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool SyntaxHighlightExists(int id)
        {
            return _context.SyntaxHighlights.Any(e => e.Id == id);
        }
    }
}