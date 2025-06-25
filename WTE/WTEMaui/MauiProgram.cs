using DataAccessLib.Data;
using DataAccessLib.Models;
using DataAccessLib.Services;
using LLMLib;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using WTEMaui.Views;
using Syncfusion.Maui.Core.Hosting;

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
                })
                .ConfigureSyncfusionCore();

            // 在 MauiProgram.cs 或 Program.cs 中
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseMySql(
                    "server=8.153.205.176;database=WTE;user=newser;password=news2048",
                    new MySqlServerVersion(new Version(8, 0, 0)));
            });

            // 注册LLM服务
            builder.Services.AddSingleton<ImageRecognitionService>(sp =>
                new ImageRecognitionService(
                    "sk-0ea4236a89b8411eb0044e4931423862", // 替换为你的API密钥
                    sp.GetService<ILogger<ImageRecognitionService>>()));

            // 注册OSS服务
            builder.Services.AddSingleton<OssService>();

            // 注册服务
            builder.Services.AddScoped<TestService>();
            builder.Services.AddScoped<UserService>();
            builder.Services.AddScoped<MealService>();
            builder.Services.AddScoped<FoodService>();
            builder.Services.AddScoped<TagService>();
            builder.Services.AddScoped<AnalysisService>();
            
            builder.Services.AddTransient<DashboardPage>(serviceProvider =>
            new DashboardPage(
                serviceProvider.GetRequiredService<ImageRecognitionService>(),
                serviceProvider.GetRequiredService<OssService>(),
                serviceProvider.GetRequiredService<MealService>(),
                serviceProvider.GetRequiredService<FoodService>(),
                serviceProvider.GetRequiredService<TagService>(),
                serviceProvider.GetRequiredService<ILogger<DashboardPage>>()));

            // 注册页面
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<AnalysisPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
