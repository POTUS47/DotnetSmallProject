using DataAccessLib.Data;
using DataAccessLib.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccessLib.Services
{
    public class FoodService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FoodService> _logger;

        public FoodService(AppDbContext context, ILogger<FoodService> logger = null)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 获取或创建食物
        /// </summary>
        public async Task<int> GetOrCreateFoodAsync(string foodName, string category = "其他")
        {
            try
            {
                // 查找是否已存在该食物
                var existingFood = await _context.Foods
                    .FirstOrDefaultAsync(f => f.Name == foodName);

                if (existingFood != null)
                {
                    _logger?.LogInformation("找到已存在的食物记录: {FoodName}, ID: {FoodId}", foodName, existingFood.FoodId);
                    return existingFood.FoodId;
                }

                // 创建新食物记录
                var newFood = new Food
                {
                    Name = foodName
                };

                _context.Foods.Add(newFood);
                await _context.SaveChangesAsync();

                _logger?.LogInformation("创建新食物记录成功: {FoodName}, ID: {FoodId}", foodName, newFood.FoodId);
                return newFood.FoodId;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取或创建食物记录失败: {FoodName}", foodName);
                throw new Exception($"处理食物记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有食物
        /// </summary>
        public async Task<List<Food>> GetAllFoodsAsync()
        {
            try
            {
                return await _context.Foods.OrderBy(f => f.Name).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取所有食物失败");
                throw new Exception($"获取食物列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据ID获取食物
        /// </summary>
        public async Task<Food> GetFoodByIdAsync(int foodId)
        {
            try
            {
                return await _context.Foods.FindAsync(foodId)
                    ?? throw new Exception("食物不存在");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取食物失败: FoodId={FoodId}", foodId);
                throw;
            }
        }

        /// <summary>
        /// 根据名称搜索食物
        /// </summary>
        public async Task<List<Food>> SearchFoodsByNameAsync(string searchTerm)
        {
            try
            {
                return await _context.Foods
                    .Where(f => f.Name.Contains(searchTerm))
                    .OrderBy(f => f.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "搜索食物失败: SearchTerm={SearchTerm}", searchTerm);
                throw new Exception($"搜索食物失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取用户历史使用过的食物
        /// </summary>
        public async Task<List<Food>> GetUserHistoryFoodsAsync(int userId, int limit = 20)
        {
            try
            {
                _logger?.LogInformation("开始获取用户历史食物: UserId={UserId}, Limit={Limit}", userId, limit);
                
                var result = await _context.MealFoodImages
                    .Where(mfi => mfi.Meal.UserId == userId)
                    .GroupBy(mfi => mfi.Food)
                    .OrderByDescending(g => g.Count()) // 按使用频率排序
                    .Take(limit)
                    .Select(g => g.Key)
                    .ToListAsync();
                
                _logger?.LogInformation("获取用户历史食物成功: UserId={UserId}, Count={Count}", userId, result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取用户历史食物失败: UserId={UserId}", userId);
                throw new Exception($"获取历史食物失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除食物
        /// </summary>
        public async Task DeleteFoodAsync(int foodId)
        {
            try
            {
                var food = await _context.Foods.FindAsync(foodId)
                    ?? throw new Exception("食物不存在");

                _context.Foods.Remove(food);
                await _context.SaveChangesAsync();

                _logger?.LogInformation("删除食物成功: FoodId={FoodId}", foodId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除食物失败: FoodId={FoodId}", foodId);
                throw;
            }
        }
    }
}
