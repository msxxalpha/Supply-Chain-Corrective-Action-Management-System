using System.Globalization;
using Indamin.Performance.Data;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Services;
public class PerformanceService(AppDbContext db){
 public static DateTime Jalali(string value){var p=value.Replace('-','/').Split('/');if(p.Length!=3)throw new ArgumentException("تاریخ شمسی نامعتبر است.");var pc=new PersianCalendar();return pc.ToDateTime(int.Parse(p[0]),int.Parse(p[1]),int.Parse(p[2]),0,0,0,0,DateTimeKind.Local);}
 public static string ToJalali(DateTime d){var pc=new PersianCalendar();return $"{pc.GetYear(d):0000}/{pc.GetMonth(d):00}/{pc.GetDayOfMonth(d):00}";}
 public async Task<EvaluationPeriod?> CurrentPeriod(){var now=DateTime.Now;return await db.Periods.Where(x=>x.IsOpen).OrderByDescending(x=>x.StartAt).FirstOrDefaultAsync(x=>x.StartAt.Date<=now.Date&&x.EndAt.Date>=now.Date);}
 public async Task<bool> CanEdit(EvaluationPeriod p){var now=DateTime.Now;return p.IsOpen&&now>=p.StartAt&&now<=p.EndAt.AddDays(1).AddTicks(-1);}
 public async Task<List<Employee>> GetDescendants(int evaluatorId){var all=await db.Employees.Where(x=>x.IsActive).ToListAsync();var result=new List<Employee>();var q=new Queue<int>();q.Enqueue(evaluatorId);while(q.Count>0){var id=q.Dequeue();foreach(var e in all.Where(x=>x.SupervisorId==id)){result.Add(e);q.Enqueue(e.Id);}}return result;}
 public async Task<bool> CanEvaluate(int evaluatorId,int employeeId){return evaluatorId!=employeeId&&(await GetDescendants(evaluatorId)).Any(x=>x.Id==employeeId);}
 public async Task<decimal> MaxScore(int positionId)=>await db.Questions.Where(x=>x.PositionId==positionId&&x.IsActive).SumAsync(x=>x.MaxScore);
 public async Task<decimal> Total(int evaluationId)=>await db.Scores.Where(x=>x.EvaluationId==evaluationId).SumAsync(x=>x.Score);
 public async Task<decimal> MaxForEvaluation(int employeeId){var e=await db.Employees.FindAsync(employeeId);return e==null?0:await MaxScore(e.PositionId);}
}