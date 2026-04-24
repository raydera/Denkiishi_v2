using Denkiishi_v2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Denkiishi_v2.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly InasDbContext _context;

    public AdminController(InasDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Metrics()
    {
        var since = DateTime.UtcNow.AddHours(-24);

        var total24h = await _context.EmailLogs
            .AsNoTracking()
            .CountAsync(e => e.CreatedAt >= since);

        var success24h = await _context.EmailLogs
            .AsNoTracking()
            .CountAsync(e => e.CreatedAt >= since && e.Status == "success");

        var error24h = await _context.EmailLogs
            .AsNoTracking()
            .CountAsync(e => e.CreatedAt >= since && e.Status == "error");

        var last5Errors = await _context.EmailLogs
            .AsNoTracking()
            .Where(e => e.Status == "error")
            .OrderByDescending(e => e.CreatedAt)
            .Take(5)
            .Select(e => new AdminEmailErrorDto
            {
                CreatedAt = e.CreatedAt,
                ToEmail = e.ToEmail,
                Subject = e.Subject,
                ErrorMessage = e.ErrorMessage
            })
            .ToListAsync();

        var model = new AdminMetricsViewModel
        {
            TotalEmailsLast24h = total24h,
            SuccessEmailsLast24h = success24h,
            ErrorEmailsLast24h = error24h,
            LastErrors = last5Errors
        };

        return View(model);
    }
}

public class AdminMetricsViewModel
{
    public int TotalEmailsLast24h { get; set; }
    public int SuccessEmailsLast24h { get; set; }
    public int ErrorEmailsLast24h { get; set; }
    public double SuccessRate => TotalEmailsLast24h == 0 ? 0 : (double)SuccessEmailsLast24h / TotalEmailsLast24h;
    public double ErrorRate => TotalEmailsLast24h == 0 ? 0 : (double)ErrorEmailsLast24h / TotalEmailsLast24h;
    public System.Collections.Generic.List<AdminEmailErrorDto> LastErrors { get; set; } = new();
}

public class AdminEmailErrorDto
{
    public DateTime CreatedAt { get; set; }
    public string ToEmail { get; set; } = "";
    public string Subject { get; set; } = "";
    public string? ErrorMessage { get; set; }
}

