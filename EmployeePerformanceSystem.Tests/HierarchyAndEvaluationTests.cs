using System.Security.Claims;
using Indamin.Performance.Controllers;
using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Indamin.PerformanceSystem.Tests;

public class HierarchyAndEvaluationTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }

    private static EvaluationController Controller(AppDbContext db, int employeeId)
    {
        var c = new EvaluationController(db, new PerformanceService(db), new ExcelService());
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("EmployeeId", employeeId.ToString())], "Test"))
            }
        };
        return c;
    }

    private static async Task<(int a, int b, int c, int d, int positionId, int periodId, int q1, int q2)> SeedScenario(AppDbContext db)
    {
        var unit = new OrgUnit { Code = "U1", Title = "واحد آزمون" };
        var position = new Position { Code = "P1", Title = "رده آزمون" };
        db.OrgUnits.Add(unit); db.Positions.Add(position); await db.SaveChangesAsync();
        var q1 = new Question { PositionId = position.Id, Text = "شاخص اول", MaxScore = 10, SortOrder = 1 };
        var q2 = new Question { PositionId = position.Id, Text = "شاخص دوم", MaxScore = 20, SortOrder = 2 };
        db.Questions.AddRange(q1, q2);
        var a = new Employee { PersonnelNo = "100", FullName = "ارزیاب ارشد", PositionId = position.Id, UnitId = unit.Id, IsEvaluator = true };
        var b = new Employee { PersonnelNo = "200", FullName = "ارزیاب میانی", PositionId = position.Id, UnitId = unit.Id, IsEvaluator = true };
        var c = new Employee { PersonnelNo = "300", FullName = "کارمند زیرمجموعه", PositionId = position.Id, UnitId = unit.Id, IsEvaluator = false };
        var d = new Employee { PersonnelNo = "400", FullName = "کارمند زیرمجموعه دوم", PositionId = position.Id, UnitId = unit.Id, IsEvaluator = false };
        db.Employees.AddRange(a, b, c, d); await db.SaveChangesAsync();
        b.SupervisorId = a.Id; c.SupervisorId = b.Id; d.SupervisorId = c.Id;
        var start = DateTime.Now.AddHours(-1); var end = DateTime.Now.AddHours(1);
        var period = new EvaluationPeriod { Title = "آزمون ماهانه", StartJalali = PerformanceService.ToJalali(start), EndJalali = PerformanceService.ToJalali(end), StartAt = start, EndAt = end, IsOpen = true };
        db.Periods.Add(period); await db.SaveChangesAsync();
        return (a.Id, b.Id, c.Id, d.Id, position.Id, period.Id, q1.Id, q2.Id);
    }

    [Fact]
    public async Task Upper_evaluator_sees_complete_subtree()
    {
        await using var db = CreateDb(); var s = await SeedScenario(db); var ps = new PerformanceService(db);
        var bTree = await ps.GetSubordinates(s.b); var aTree = await ps.GetSubordinates(s.a);
        Assert.Equal(2, bTree.Count); Assert.Contains(bTree, x => x.Id == s.c); Assert.Contains(bTree, x => x.Id == s.d);
        Assert.Equal(3, aTree.Count); Assert.Contains(aTree, x => x.Id == s.b); Assert.Contains(aTree, x => x.Id == s.c); Assert.Contains(aTree, x => x.Id == s.d);
    }

    [Fact]
    public async Task Previous_evaluator_scores_are_visible_and_upper_evaluator_can_change_them_with_history()
    {
        await using var db = CreateDb(); var s = await SeedScenario(db);
        var first = await Controller(db, s.b).Form(new EvaluationController.FormPost
        {
            EmployeeId = s.c, PositionId = s.positionId,
            Scores = new Dictionary<int, decimal> { [s.q1] = 8, [s.q2] = 18 },
            Comments = new Dictionary<int, string> { [s.q1] = "ارزیابی اولیه" }
        });
        Assert.IsType<RedirectToActionResult>(first);
        var ev = await db.Evaluations.Include(x => x.Scores).SingleAsync(x => x.PeriodId == s.periodId && x.EmployeeId == s.c);
        Assert.Equal(s.b, ev.EvaluatorId); Assert.Equal(s.b, ev.OriginalEvaluatorId); Assert.Equal(26m, ev.Scores.Sum(x => x.Score));

        var second = await Controller(db, s.a).Form(new EvaluationController.FormPost
        {
            EmployeeId = s.c, PositionId = s.positionId,
            Scores = new Dictionary<int, decimal> { [s.q1] = 9, [s.q2] = 20 },
            Comments = new Dictionary<int, string> { [s.q1] = "بازنگری ارزیاب بالادست" }
        });
        Assert.IsType<RedirectToActionResult>(second);
        ev = await db.Evaluations.Include(x => x.Scores).SingleAsync(x => x.Id == ev.Id);
        Assert.Equal(s.a, ev.EvaluatorId); Assert.Equal(s.b, ev.OriginalEvaluatorId); Assert.Equal(29m, ev.Scores.Sum(x => x.Score));
        var scoreHistory = await db.ScoreHistory.Where(x => x.EvaluationId == ev.Id).OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, scoreHistory.Count); Assert.Equal(8m, scoreHistory[0].OldScore); Assert.Equal(9m, scoreHistory[0].NewScore); Assert.Equal(18m, scoreHistory[1].OldScore); Assert.Equal(20m, scoreHistory[1].NewScore); Assert.Equal(s.a, scoreHistory[0].ChangedBy);
        var evaluatorHistory = await db.EvaluatorHistory.SingleAsync(x => x.EvaluationId == ev.Id);
        Assert.Equal(s.b, evaluatorHistory.PreviousEvaluatorId); Assert.Equal(s.a, evaluatorHistory.NewEvaluatorId); Assert.Equal(s.a, evaluatorHistory.ChangedBy);
    }

    [Fact]
    public async Task Lower_evaluator_cannot_overwrite_after_upper_level_takeover()
    {
        await using var db = CreateDb(); var s = await SeedScenario(db);
        await Controller(db, s.b).Form(new EvaluationController.FormPost { EmployeeId = s.c, PositionId = s.positionId, Scores = new Dictionary<int, decimal> { [s.q1] = 8, [s.q2] = 18 } });
        await Controller(db, s.a).Form(new EvaluationController.FormPost { EmployeeId = s.c, PositionId = s.positionId, Scores = new Dictionary<int, decimal> { [s.q1] = 9, [s.q2] = 20 } });
        var result = await Controller(db, s.b).Form(new EvaluationController.FormPost { EmployeeId = s.c, PositionId = s.positionId, Scores = new Dictionary<int, decimal> { [s.q1] = 5, [s.q2] = 5 } });
        Assert.IsType<ForbidResult>(result);
        var ev = await db.Evaluations.Include(x => x.Scores).SingleAsync(x => x.PeriodId == s.periodId && x.EmployeeId == s.c);
        Assert.Equal(29m, ev.Scores.Sum(x => x.Score)); Assert.Equal(s.a, ev.EvaluatorId);
    }

    [Fact]
    public async Task Non_evaluator_cannot_submit_an_evaluation()
    {
        await using var db = CreateDb(); var s = await SeedScenario(db);
        var result = await Controller(db, s.c).Form(new EvaluationController.FormPost { EmployeeId = s.d, PositionId = s.positionId, Scores = new Dictionary<int, decimal> { [s.q1] = 10, [s.q2] = 20 } });
        Assert.IsType<ForbidResult>(result); Assert.Empty(await db.Evaluations.ToListAsync());
    }

    [Fact]
    public async Task Score_above_question_maximum_is_rejected_and_not_saved()
    {
        await using var db = CreateDb(); var s = await SeedScenario(db);
        var result = await Controller(db, s.b).Form(new EvaluationController.FormPost { EmployeeId = s.c, PositionId = s.positionId, Scores = new Dictionary<int, decimal> { [s.q1] = 11, [s.q2] = 20 } });
        Assert.IsType<ViewResult>(result); Assert.Empty(await db.Evaluations.ToListAsync()); Assert.Empty(await db.ScoreHistory.ToListAsync());
    }
}