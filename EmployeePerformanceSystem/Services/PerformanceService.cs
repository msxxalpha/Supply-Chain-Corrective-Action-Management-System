using System.Globalization;
using Indamin.Performance.Data;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Services;
public class PerformanceService(AppDbContext db){
 public static DateTime Jalali(string value){var p=value.Replace('-','/').Split('/');if(p.Length!=3)throw new ArgumentException("تاریخ شمسی نامعتبر است.");var pc=new PersianCalendar();return pc.ToDateTime(int.Parse(p[0]),int.Parse(p[1]),int.Parse(p[2]),0,0,0,0,DateTimeKind.Local);}
 public static string ToJalali(DateTime d){var pc=new PersianCalendar();return $"{pc.GetYear(d):0000}/{pc.GetMonth(d):00}/{pc.GetDayOfMonth(d):00}";}
 public async Task<EvaluationPeriod?> CurrentPeriod(){var now=DateTime.Now;return await db.Periods.Where(x=>x.IsOpen&&x.StartAt<=now).OrderByDescending(x=>x.StartAt).FirstOrDefaultAsync();}
 public Task<bool> CanEdit(EvaluationPeriod p)=>Task.FromResult(p.IsOpen&&DateTime.Now>=p.StartAt&&DateTime.Now<=p.EndAt);
 public async Task<List<Employee>> GetDirectSubordinates(int evaluatorId)=>await db.Employees.Where(x=>x.IsActive&&x.SupervisorId==evaluatorId).Include(x=>x.Position).Include(x=>x.Unit).OrderBy(x=>x.FullName).ToListAsync();
 public async Task<bool> CanEvaluate(int evaluatorId,int employeeId)=>evaluatorId!=employeeId&&await db.Employees.AnyAsync(x=>x.Id==employeeId&&x.IsActive&&x.SupervisorId==evaluatorId);
 public async Task<bool> CanReviewEvaluation(int actorId,Evaluation ev)=>ev.EvaluatorId==actorId||await db.Employees.AnyAsync(x=>x.Id==ev.EvaluatorId&&x.SupervisorId==actorId&&x.IsEvaluator&&x.IsActive);
 public async Task<decimal> MaxScore(int positionId)=>await db.Questions.Where(x=>x.PositionId==positionId&&x.IsActive).SumAsync(x=>x.MaxScore);
 public async Task<decimal> Total(int evaluationId)=>await db.Scores.Where(x=>x.EvaluationId==evaluationId).SumAsync(x=>x.Score);
 public async Task<decimal> MaxForEvaluation(int employeeId){var e=await db.Employees.FindAsync(employeeId);return e==null?0:await MaxScore(e.PositionId);}
}