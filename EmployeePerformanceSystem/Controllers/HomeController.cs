using System.Security.Claims;
using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;
[Authorize] public class HomeController(AppDbContext db,PerformanceService ps):Controller{
 public async Task<IActionResult> Index(){var uid=int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);var u=await db.Users.FindAsync(uid);if(u!.IsAdmin)return View(new HomeVm("مدیر سیستم",0,0));var eid=int.Parse(User.FindFirstValue("EmployeeId")!);var sub=await ps.GetDescendants(eid);var p=await ps.CurrentPeriod();var done=p==null?0:await db.Evaluations.CountAsync(x=>x.PeriodId==p.Id&&sub.Select(s=>s.Id).Contains(x.EmployeeId));return View(new HomeVm(User.Identity!.Name!,sub.Count,done));}
 public IActionResult Error()=>View(); public record HomeVm(string Name,int Subordinates,int Completed);
}