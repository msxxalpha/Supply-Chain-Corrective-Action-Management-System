using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;
[Authorize(Policy="AdminOnly")] public class ReportsController(AppDbContext db,ExcelService excel):Controller{
 public async Task<IActionResult> Index(){return View(await db.Evaluations.Include(x=>x.Scores).ToListAsync());}
}