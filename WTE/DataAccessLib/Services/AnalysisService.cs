using DataAccessLib.Data;
using DataAccessLib.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccessLib.Services
{
    public class AnalysisService
    {
        private readonly AppDbContext _context;
        public AnalysisService(AppDbContext context)
        {
            _context = context;
        }

        // 日历统计DTO
        public class DailyStatDto
        {
            public DateOnly Date { get; set; }
            public List<string> Foods { get; set; } = new();
            public double TotalCalories { get; set; } // 预留
            public double TotalProtein { get; set; } // 预留
        }

        // 获取某用户一段时间内每天的食物/热量/蛋白质统计
        public async Task<List<DailyStatDto>> GetUserDailyStatsAsync(int userId, DateOnly start, DateOnly end)
        {
            var meals = await _context.Meals
                .Where(m => m.UserId == userId && m.MealDate >= start && m.MealDate <= end)
                .Include(m => m.MealFoodImages)
                    .ThenInclude(mfi => mfi.Food)
                .ToListAsync();

            var result = new List<DailyStatDto>();
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var dayMeals = meals.Where(m => m.MealDate == date).ToList();
                var foods = dayMeals.SelectMany(m => m.MealFoodImages.Select(mfi => mfi.Food.Name)).Distinct().ToList();
                // 目前Food没有营养素字段，热量/蛋白质统计为0，后续可扩展
                result.Add(new DailyStatDto
                {
                    Date = date,
                    Foods = foods,
                    TotalCalories = 0,
                    TotalProtein = 0
                });
            }
            return result;
        }

        // 预留：获取健康建议（可对接大模型API）
        public async Task<string> GetHealthAdviceAsync(int userId, DateOnly start, DateOnly end)
        {
            // 这里可调用大模型API，传入统计数据，返回建议
            return "健康建议功能开发中，可对接大模型API";
        }
    }
} 