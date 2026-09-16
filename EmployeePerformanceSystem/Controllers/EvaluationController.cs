using System.Security.Claims;
using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;

[Authorize]
public class EvaluationController(AppDbContext db, PerformanceService ps, ExcelService excel) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (!int.TryParse(User.FindFirstValue("EmployeeId"), out var eid)) return Forbid();
        if (!await ps.IsEvaluator(eid)) return Forbid();
        var p = await ps.CurrentPeriod();
        if (p == null) return View(new List<Row>());
        var employees = await ps.GetSubordinates(eid);
        var rows = employees.Select(x => new Row(x.Id, x.FullName, x.PersonnelNo, x.Position?.Title ?? "", x.Unit?.Title ?? "", x.IsEvaluator)).ToList();
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> ExportMyList()
    {
        if (!int.TryParse(User.FindFirstValue("EmployeeId"), out var eid) || !await ps.IsEvaluator(eid)) return Forbid();
        var rows = await ps.GetSubordinates(eid);
        return File(excel.Employees(rows), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "MySubordinates.xlsx");
    }

    public record Row(int Id, string Name, string PersonnelNo, string Position, string Unit, bool IsEvaluator);

    [HttpGet]
    public async Task<IActionResult> Form(int id)
    {
        if (!int.TryParse(User.FindFirstValue("EmployeeId"), out var actor) || !await ps.IsEvaluator(actor)) return Forbid();
        var p = await ps.CurrentPeriod();
        if (p == null) return NotFound();
        var employee = await db.Employees.Include(x => x.Position).FirstOrDefaultAsync(x => x.Id == id);
        if (employee == null || !await ps.CanEvaluate(actor, id)) return Forbid();

        var ev = await db.Evaluations.Include(x => x.Scores)
            .SingleOrDefaultAsync(x => x.PeriodId == p.Id && x.EmployeeId == id);
        var qs = await db.Questions.Where(x => x.PositionId == employee.PositionId && x.IsActive)
            .OrderBy(x => x.SortOrder).ToListAsync();
        return View(new FormVm(p, employee, qs, ev));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Form(FormPost m)
    {
        if (!int.TryParse(User.FindFirstValue("EmployeeId"), out var actor) || !await ps.IsEvaluator(actor)) return Forbid();
        var p = await ps.CurrentPeriod();
        if (p == null) return NotFound();
        if (!await ps.CanEdit(p)) return BadRequest("بازه ارزیابی پایان یافته است و امکان تغییر وجود ندارد.");

        var employee = await db.Employees.Include(x => x.Position).FirstOrDefaultAsync(x => x.Id == m.EmployeeId && x.IsActive);
        if (employee == null) return NotFound();
        if (!await ps.CanEvaluate(actor, employee.Id)) return Forbid();

        var ev = await db.Evaluations.Include(x => x.Scores)
            .SingleOrDefaultAsync(x => x.PeriodId == p.Id && x.EmployeeId == employee.Id);
        if (ev == null)
        {
            ev = new Evaluation
            {
                PeriodId = p.Id,
                EmployeeId = employee.Id,
                EvaluatorId = actor,
                OriginalEvaluatorId = actor,
                Status = EvaluationStatus.Draft
            };
            db.Evaluations.Add(ev);
            await db.SaveChangesAsync();
        }
        else if (!await ps.CanReviewEvaluation(actor, ev)) return Forbid();

        var qs = await db.Questions.Where(x => x.PositionId == employee.PositionId && x.IsActive)
            .OrderBy(x => x.SortOrder).ToListAsync();
        foreach (var q in qs)
        {
            var score = m.Scores.TryGetValue(q.Id, out var suppliedScore) ? suppliedScore : 0m;
            if (score < 0 || score > q.MaxScore)
            {
                ModelState.AddModelError($"Scores[{q.Id}]", $"امتیاز سؤال «{q.Text}» باید بین صفر و {q.MaxScore} باشد.");
                continue;
            }

            var comment = m.Comments.TryGetValue(q.Id, out var c) ? c?.Trim() : null;
            var old = ev.Scores.FirstOrDefault(x => x.QuestionId == q.Id);
            if (old == null)
            {
                ev.Scores.Add(new EvaluationScore { QuestionId = q.Id, Score = score, Comment = comment });
            }
            else if (old.Score != score || old.Comment != comment)
            {
                db.ScoreHistory.Add(new EvaluationScoreHistory
                {
                    EvaluationId = ev.Id,
                    QuestionId = q.Id,
                    OldScore = old.Score,
                    NewScore = score,
                    ChangedBy = actor,
                    Reason = ev.EvaluatorId == actor ? "اصلاح ارزیابی" : "بازنگری ارزیاب بالادست"
                });
                old.Score = score;
                old.Comment = comment;
                old.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (!ModelState.IsValid)
        {
            var existing = await db.Evaluations.AsNoTracking().Include(x => x.Scores).SingleAsync(x => x.Id == ev.Id);
            return View("Form", new FormVm(p, employee, qs, existing));
        }

        if (ev.EvaluatorId != actor)
        {
            db.EvaluatorHistory.Add(new EvaluatorChangeHistory
            {
                EvaluationId = ev.Id,
                PreviousEvaluatorId = ev.EvaluatorId,
                NewEvaluatorId = actor,
                ChangedBy = actor,
                Reason = "بازنگری ارزیاب بالادست"
            });
            ev.EvaluatorId = actor;
        }

        ev.Status = EvaluationStatus.Submitted;
        ev.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public record FormVm(EvaluationPeriod Period, Employee Employee, List<Question> Questions, Evaluation? Evaluation);
    public class FormPost
    {
        public int EmployeeId { get; set; }
        public int PositionId { get; set; }
        public Dictionary<int, decimal> Scores { get; set; } = [];
        public Dictionary<int, string> Comments { get; set; } = [];
    }
}