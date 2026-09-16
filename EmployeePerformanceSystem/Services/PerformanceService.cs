using System.Globalization;
using Indamin.Performance.Data;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Services;
public class PerformanceService(AppDbContext db)
{
    public static DateTime Jalali(string value)
    {
        var p = value.Replace('-', '/').Split('/');
        if (p.Length != 3) throw new ArgumentException("تاریخ شمسی نامعتبر است.");
        var pc = new PersianCalendar();
        return pc.ToDateTime(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), 0, 0, 0, 0, DateTimeKind.Local);
    }

    public static string ToJalali(DateTime d)
    {
        var pc = new PersianCalendar();
        return $"{pc.GetYear(d):0000}/{pc.GetMonth(d):00}/{pc.GetDayOfMonth(d):00}";
    }

    public async Task<EvaluationPeriod?> CurrentPeriod()
        => await db.Periods.Where(x => x.IsOpen && x.StartAt <= DateTime.Now)
            .OrderByDescending(x => x.StartAt).FirstOrDefaultAsync();

    public Task<bool> CanEdit(EvaluationPeriod p)
        => Task.FromResult(p.IsOpen && DateTime.Now >= p.StartAt && DateTime.Now <= p.EndAt);

    /// <summary>
    /// Returns the complete organizational subtree below an evaluator.
    /// This is intentional: an evaluator who is higher in the hierarchy than another
    /// evaluator must be able to review/evaluate that evaluator's complete subtree.
    /// </summary>
    public async Task<List<Employee>> GetSubordinates(int evaluatorId)
    {
        var employees = await db.Employees.Where(x => x.IsActive)
            .Include(x => x.Position).Include(x => x.Unit).AsNoTracking().ToListAsync();
        var bySupervisor = employees.Where(x => x.SupervisorId.HasValue)
            .GroupBy(x => x.SupervisorId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<Employee>();
        var queue = new Queue<int>();
        queue.Enqueue(evaluatorId);
        var visited = new HashSet<int> { evaluatorId };
        while (queue.Count > 0)
        {
            var managerId = queue.Dequeue();
            if (!bySupervisor.TryGetValue(managerId, out var children)) continue;
            foreach (var child in children)
            {
                if (!visited.Add(child.Id)) continue;
                result.Add(child);
                queue.Enqueue(child.Id);
            }
        }
        return result.OrderBy(x => x.FullName).ToList();
    }

    public Task<List<Employee>> GetDirectSubordinates(int evaluatorId)
        => GetSubordinates(evaluatorId).ContinueWith(t => t.Result.Where(x => x.SupervisorId == evaluatorId).ToList());

    public async Task<bool> CanEvaluate(int evaluatorId, int employeeId)
    {
        if (evaluatorId == employeeId) return false;
        return (await GetSubordinates(evaluatorId)).Any(x => x.Id == employeeId);
    }

    /// <summary>
    /// The current evaluator or any evaluator above the current evaluator in the
    /// organization tree may review the evaluation. The actor must be an evaluator.
    /// </summary>
    public async Task<bool> CanReviewEvaluation(int actorId, Evaluation ev)
    {
        if (actorId == ev.EvaluatorId) return await IsEvaluator(actorId);
        if (!await IsEvaluator(actorId)) return false;
        return await IsAncestor(actorId, ev.EmployeeId);
    }

    public async Task<bool> IsEvaluator(int employeeId)
        => await db.Employees.AnyAsync(x => x.Id == employeeId && x.IsActive && x.IsEvaluator);

    public async Task<bool> IsAncestor(int ancestorId, int employeeId)
    {
        var employees = await db.Employees.Where(x => x.IsActive)
            .Select(x => new { x.Id, x.SupervisorId }).AsNoTracking().ToListAsync();
        var map = employees.ToDictionary(x => x.Id, x => x.SupervisorId);
        var current = employeeId;
        var visited = new HashSet<int>();
        while (map.TryGetValue(current, out var supervisorId) && supervisorId.HasValue)
        {
            if (!visited.Add(current)) return false;
            if (supervisorId.Value == ancestorId) return true;
            current = supervisorId.Value;
        }
        return false;
    }

    public async Task<decimal> MaxScore(int positionId)
        => await db.Questions.Where(x => x.PositionId == positionId && x.IsActive).SumAsync(x => x.MaxScore);

    public async Task<decimal> Total(int evaluationId)
        => await db.Scores.Where(x => x.EvaluationId == evaluationId).SumAsync(x => x.Score);

    public async Task<decimal> MaxForEvaluation(int employeeId)
    {
        var e = await db.Employees.FindAsync(employeeId);
        return e == null ? 0 : await MaxScore(e.PositionId);
    }
}