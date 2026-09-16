using Indamin.Performance.Data;
using Microsoft.EntityFrameworkCore;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(o=>o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<PerformanceService>();
var app=builder.Build();
if(!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles(); app.UseRouting(); app.MapRazorPages(); app.Run();