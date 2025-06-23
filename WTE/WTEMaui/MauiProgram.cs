using DataAccessLib.Data;
using DataAccessLib.Models;
using DataAccessLib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;

namespace WTEMaui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // 在 MauiProgram.cs 或 Program.cs 中
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseMySql(
                    "server=8.153.205.176;database=WTE;user=newser;password=news2048",
                    new MySqlServerVersion(new Version(8, 0, 0)));
            });

            builder.Services.AddScoped<TestService>();
            builder.Services.AddTransient<MainPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
