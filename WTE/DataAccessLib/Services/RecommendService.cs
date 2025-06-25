using DataAccessLib.Data;
using DataAccessLib.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccessLib.Services
{
    public class RecommendService
    {
        private readonly AppDbContext _context;
        public RecommendService(AppDbContext context)
        {
            _context = context;
        }

        // 随机推荐一个食物（避免连续重复）
        public async Task<Food?> GetRandomFoodAsync(int userId, List<int> recentFoodIds = null)
        {
            var foods = await _context.Foods.ToListAsync();
            if (recentFoodIds != null && recentFoodIds.Count > 0)
            {
                foods = foods.Where(f => !recentFoodIds.Contains(f.FoodId)).ToList();
            }
            if (foods.Count == 0) return null;
            var rand = new Random();
            return foods[rand.Next(foods.Count)];
        }

        // 健康推荐（简单示例：优先推荐带"蔬菜"标签的食物）
        public async Task<Food?> GetHealthyFoodAsync(int userId)
        {
            var foods = await _context.Foods.ToListAsync();
            var healthy = foods.FirstOrDefault(f => f.Name.Contains("蔬菜") || f.Name.Contains("水果"));
            return healthy ?? foods.FirstOrDefault();
        }
    }
} 