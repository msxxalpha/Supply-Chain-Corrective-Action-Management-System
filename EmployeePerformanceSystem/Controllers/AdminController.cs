using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;
[Authorize(Policy="AdminOnly")] public class AdminController(AppDbContext db):Controller{
 public async Task<IActionResult> Periods()=>View(await db.Periods.OrderByDescending(x=>x.StartAt).ToListAsync());
 [HttpPost][ValidateAntiForgeryToken]public async Task<IActionResult>CreatePeriod(string title,string startJalali,string endJalali,string? description){var s=PerformanceService.Jalali(startJalali);var e=PerformanceService.Jalali(endJalali).AddDays(1).AddTicks(-1);db.Periods.Add(new EvaluationPeriod{Title=title,StartJalali=startJalali,EndJalali=endJalali,StartAt=s,EndAt=e,Description=description,IsOpen=true});await db.SaveChangesAsync();return RedirectToAction(nameof(Periods));}
}