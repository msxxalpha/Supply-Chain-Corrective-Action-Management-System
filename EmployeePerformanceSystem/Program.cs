using System.Security.Cryptography;
using System.Text;
using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<PerformanceService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => { o.LoginPath="/Account/Login"; o.AccessDeniedPath="/Account/Denied"; o.ExpireTimeSpan=TimeSpan.FromHours(8); o.SlidingExpiration=true; });
builder.Services.AddAuthorization();
var app=builder.Build();
using(var scope=app.Services.CreateScope()){ var db=scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.EnsureCreatedAsync(); await Seed.Initialize(db); }
if(!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute(name:"default",pattern:"{controller=Home}/{action=Index}/{id?}");
app.Run();

static class Seed { public static async Task Initialize(AppDbContext db){ if(await db.Users.AnyAsync()) return; var admin=new AppUser{UserName="admin",DisplayName="مدیر سیستم",EmployeeId=null,IsAdmin=true,IsActive=true,PasswordHash=PasswordHasher.Hash("ChangeMe123!")}; db.Users.Add(admin); await db.SaveChangesAsync(); } }
static class PasswordHasher { public static string Hash(string value){ using var h=SHA256.Create(); return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(value))); } public static bool Verify(string value,string hash)=>Hash(value)==hash; }