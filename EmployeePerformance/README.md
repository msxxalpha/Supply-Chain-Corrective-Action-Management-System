# سامانه ارزیابی عملکرد ماهیانه کارکنان

سامانه تحت وب ارزیابی عملکرد ماهیانه کارکنان با ASP.NET Core، SQL Server، Entity Framework Core، Razor Pages/MVC، Bootstrap و HTMX.

## معماری
- Modular Monolith
- ASP.NET Core 10 LTS / C#
- Entity Framework Core + SQL Server
- ASP.NET Core Identity
- Bootstrap RTL + Vazirmatn/Vazir font
- تقویم هجری شمسی
- ClosedXML برای ورود/خروج Excel

## قابلیت‌ها
- تعریف دوره ارزیابی با تاریخ شمسی
- فعال بودن ورود ارزیاب فقط در بازه ارزیابی
- ساختار سازمانی درختی و تعیین ارزیاب
- رده‌های پستی و پرسش‌های اختصاصی هر رده با سقف امتیاز
- ارزیابی کارکنان زیرمجموعه ارزیاب
- امکان بازنگری امتیاز ارزیاب توسط ارزیاب بالادست با ثبت تاریخچه کامل
- محاسبه امتیاز نهایی و درصد تحقق
- ورود گروهی کارکنان از Excel با اعتبارسنجی
- خروجی Excel، فیلتر و مرتب‌سازی در فهرست‌ها و گزارش‌ها
- گزارش عملکرد فردی، واحدی، ارزیابان و دوره
- Audit Trail برای تغییرات حساس

## ساختار پروژه
`src/Indamin.Performance.Web` شامل وب‌اپ، Domain، Data و Services است.

## راه‌اندازی
1. SQL Server را آماده کنید.
2. Connection String را در `appsettings.json` قرار دهید.
3. `dotnet restore`
4. `dotnet ef database update`
5. `dotnet run`

> این نسخه اسکلت اجرایی و هسته MVP است و برای استقرار سازمانی باید Connection String، سیاست رمز عبور، HTTPS، پشتیبان‌گیری و داده‌های واقعی سازمان تنظیم شوند.
