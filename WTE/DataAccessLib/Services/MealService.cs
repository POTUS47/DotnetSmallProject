using DataAccessLib.Config;
using DataAccessLib.Data;
using DataAccessLib.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DataAccessLib.Services
{
    public class MealService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MealService> _logger;
        private readonly OssService _ossService;

        public MealService(AppDbContext context, OssService ossService, ILogger<MealService> logger = null)
        {
            _context = context;
            _ossService = ossService;
            _logger = logger;
        }

        #region 餐食记录管理
        /// <summary>
        /// 添加餐食记录
        /// </summary>
        public async Task<Meal> AddMealAsync(int userId, string mealType, DateOnly mealDate, TimeOnly mealTime)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new Exception("用户不存在");

            var meal = new Meal
            {
                UserId = userId,
                MealType = mealType,
                MealDate = mealDate,
                MealTime = mealTime
            };

            _context.Meals.Add(meal);
            await _context.SaveChangesAsync();
            
            _logger?.LogInformation("用户 {UserId} 添加了餐食记录: {MealType} - {MealDate} {MealTime}", 
                userId, mealType, mealDate, mealTime);
            
            return meal;
        }

        /// <summary>
        /// 为餐食添加食物（带图片上传）
        /// </summary>
        public async Task AddFoodToMealWithImageAsync(int mealId, int foodId, string? localImagePath = null)
        {
            var meal = await _context.Meals.FindAsync(mealId)
                ?? throw new Exception("餐食记录不存在");

            var food = await _context.Foods.FindAsync(foodId)
                ?? throw new Exception("食物不存在");

            string? ossImagePath = null;

            // 如果提供了本地图片路径，上传到OSS
            if (!string.IsNullOrEmpty(localImagePath) && File.Exists(localImagePath))
            {
                try
                {
                    var fileName = Path.GetFileName(localImagePath);
                    var fileExtension = Path.GetExtension(fileName);
                    var ossObjectName = localImagePath;
                    
                    _ossService.UploadFile(ossObjectName, localImagePath);
                    ossImagePath = ossObjectName;
                    
                    _logger?.LogInformation("成功上传图片到OSS: {OssPath}", ossObjectName);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "上传图片到OSS失败: {LocalPath}", localImagePath);
                    throw new Exception($"图片上传失败: {ex.Message}");
                }
            }

            // 检查是否已经存在该食物记录
            var existingRecord = await _context.MealFoodImages
                .FirstOrDefaultAsync(mfi => mfi.MealId == mealId && mfi.FoodId == foodId);

            if (existingRecord != null)
            {
                // 如果存在旧图片，先删除OSS中的旧图片
                if (!string.IsNullOrEmpty(existingRecord.ImagePath))
                {
                    await DeleteImageFromOssAsync(existingRecord.ImagePath);
                }
                // 更新图片路径
                existingRecord.ImagePath = ossImagePath;
            }
            else
            {
                // 创建新记录
                var mealFoodImage = new MealFoodImage
                {
                    MealId = mealId,
                    FoodId = foodId,
                    ImagePath = ossImagePath
                };
                _context.MealFoodImages.Add(mealFoodImage);
            }

            await _context.SaveChangesAsync();
            _logger?.LogInformation("为餐食 {MealId} 添加了食物 {FoodId}", mealId, foodId);
        }

        /// <summary>
        /// 为餐食添加食物
        /// </summary>
        public async Task AddFoodToMealAsync(int mealId, int foodId, string? imagePath = null)
        {
            var meal = await _context.Meals.FindAsync(mealId)
                ?? throw new Exception("餐食记录不存在");

            var food = await _context.Foods.FindAsync(foodId)
                ?? throw new Exception("食物不存在");

            // 检查是否已经存在该食物记录
            var existingRecord = await _context.MealFoodImages
                .FirstOrDefaultAsync(mfi => mfi.MealId == mealId && mfi.FoodId == foodId);

            if (existingRecord != null)
            {
                // 更新图片路径
                existingRecord.ImagePath = imagePath;
            }
            else
            {
                // 创建新记录
                var mealFoodImage = new MealFoodImage
                {
                    MealId = mealId,
                    FoodId = foodId,
                    ImagePath = imagePath
                };
                _context.MealFoodImages.Add(mealFoodImage);
            }

            await _context.SaveChangesAsync();
            _logger?.LogInformation("为餐食 {MealId} 添加了食物 {FoodId}", mealId, foodId);
        }

        /// <summary>
        /// 为餐食中的食物添加标签
        /// </summary>
        public async Task AddTagToMealFoodAsync(int mealId, int foodId, int tagId)
        {
            var meal = await _context.Meals.FindAsync(mealId)
                ?? throw new Exception("餐食记录不存在");

            var food = await _context.Foods.FindAsync(foodId)
                ?? throw new Exception("食物不存在");

            var tag = await _context.Tags.FindAsync(tagId)
                ?? throw new Exception("标签不存在");

            // 检查是否已经存在该标签
            var existingTag = await _context.MealFoodTags
                .FirstOrDefaultAsync(mft => mft.MealId == mealId && mft.FoodId == foodId && mft.TagId == tagId);

            if (existingTag != null)
                throw new Exception("该标签已存在");

            var mealFoodTag = new MealFoodTag
            {
                MealId = mealId,
                FoodId = foodId,
                TagId = tagId
            };

            _context.MealFoodTags.Add(mealFoodTag);
            await _context.SaveChangesAsync();
            
            _logger?.LogInformation("为餐食 {MealId} 的食物 {FoodId} 添加了标签 {TagId}", mealId, foodId, tagId);
        }
        #endregion

        #region 餐食查询

        /// <summary>
        /// 获取用户详细饮食数据JSON（包含食物名称和标签）
        /// </summary>
        public async Task<string> GetUserDetailedMealsJsonAsync(int userId, int dayCount = 30)
        {
            try
            {
                _logger?.LogInformation("正在查询用户 {UserId} 的详细饮食记录", userId);

                var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-dayCount));
                var endDate = DateOnly.FromDateTime(DateTime.Today);

                // 分步骤查询以避免复杂的 LINQ 翻译问题
                // 1. 先获取基本的餐食数据
                var meals = await _context.Meals
                    .Where(m => m.UserId == userId && m.MealDate >= startDate && m.MealDate <= endDate)
                    .OrderByDescending(m => m.MealDate)
                    .ThenBy(m => m.MealTime)
                    .ToListAsync();

                var mealsData = new List<object>();

                // 2. 为每个餐食单独查询食物和标签信息
                foreach (var meal in meals)
                {
                    // 获取该餐食的食物信息
                    var mealFoods = await _context.MealFoodImages
                        .Where(mfi => mfi.MealId == meal.MealId)
                        .Include(mfi => mfi.Food)
                        .ToListAsync();

                    var foods = new List<object>();

                    foreach (var mealFood in mealFoods)
                    {
                        // 获取该食物的标签
                        var tags = await _context.MealFoodTags
                            .Where(mft => mft.MealId == meal.MealId && mft.FoodId == mealFood.FoodId)
                            .Include(mft => mft.Tag)
                            .Select(mft => mft.Tag.TagName)
                            .ToListAsync();

                        foods.Add(new
                        {
                            FoodName = mealFood.Food.Name,
                            Tags = tags
                        });
                    }

                    mealsData.Add(new
                    {
                        MealType = meal.MealType,
                        MealDate = meal.MealDate,
                        MealTime = meal.MealTime.ToString(@"HH\:mm"),
                        Foods = foods
                    });
                }

                // 序列化为JSON
                return JsonSerializer.Serialize(mealsData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "查询详细饮食记录失败");
                throw new Exception("获取详细饮食数据失败，请检查网络连接", ex);
            }
        }

        /// <summary>
        /// 获取用户详细饮食数据JSON（优化版本）
        /// </summary>
        public async Task<string> GetUserDetailedMealsJsonOptimizedAsync(int userId, int dayCount = 30)
        {
            try
            {
                _logger?.LogInformation("正在查询用户 {UserId} 的详细饮食记录（优化版本）", userId);

                var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-dayCount));
                var endDate = DateOnly.FromDateTime(DateTime.Today);

                // 1. 获取基本餐食数据
                var meals = await _context.Meals
                    .Where(m => m.UserId == userId && m.MealDate >= startDate && m.MealDate <= endDate)
                    .OrderByDescending(m => m.MealDate)
                    .ThenBy(m => m.MealTime)
                    .ToListAsync();

                var mealIds = meals.Select(m => m.MealId).ToList();

                // 2. 批量获取所有相关的食物信息
                var allMealFoods = await _context.MealFoodImages
                    .Where(mfi => mealIds.Contains(mfi.MealId))
                    .Include(mfi => mfi.Food)
                    .ToListAsync();

                // 3. 批量获取所有相关的标签信息
                var allMealTags = await _context.MealFoodTags
                    .Where(mft => mealIds.Contains(mft.MealId))
                    .Include(mft => mft.Tag)
                    .ToListAsync();

                // 4. 在内存中组装数据
                var mealsData = meals.Select(meal => new
                {
                    MealType = meal.MealType,
                    MealDate = meal.MealDate,
                    MealTime = meal.MealTime.ToString(@"HH\:mm"),
                    Foods = allMealFoods
                        .Where(mfi => mfi.MealId == meal.MealId)
                        .Select(mfi => new
                        {
                            FoodName = mfi.Food.Name,
                            Tags = allMealTags
                                .Where(mft => mft.MealId == meal.MealId && mft.FoodId == mfi.FoodId)
                                .Select(mft => mft.Tag.TagName)
                                .ToList()
                        })
                        .ToList()
                }).ToList();

                // 序列化为JSON
                return JsonSerializer.Serialize(mealsData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "查询详细饮食记录失败（优化版本）");
                throw new Exception("获取详细饮食数据失败，请检查网络连接", ex);
            }
        }

        /// <summary>
        /// 获取当前用户的饮食记录JSON
        /// </summary>
        public async Task<string> GetUserMealsJsonAsync(int userId)
        {
            try
            {
                _logger?.LogInformation("正在查询用户 {UserId} 的饮食记录", userId);

                // 查询数据库
                var meals = await _context.Meals
                    .Where(m => m.UserId == userId)
                    .OrderByDescending(m => m.MealDate)
                    .ThenBy(m => m.MealTime)
                    .Select(m => new
                    {
                        MealType = m.MealType.ToString(), // 转换枚举为字符串
                        m.MealDate,
                        MealTime = m.MealTime.ToString(@"HH\:mm") // 格式化时间
                    })
                    .ToListAsync();

                // 序列化为JSON
                return JsonSerializer.Serialize(meals, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "查询饮食记录失败");
                throw new Exception("获取饮食数据失败，请检查网络连接", ex);
            }
        }




        /// <summary>
        /// 获取用户某日的所有餐食记录
        /// </summary>
        public async Task<List<Meal>> GetUserMealsByDateAsync(int userId, DateOnly date)
        {
            return await _context.Meals
                .Where(m => m.UserId == userId && m.MealDate == date)
                .Include(m => m.MealFoodImages)
                    .ThenInclude(mfi => mfi.Food)
                .Include(m => m.MealFoodTags)
                    .ThenInclude(mft => mft.Tag)
                .OrderBy(m => m.MealTime)
                .ToListAsync();
        }

        /// <summary>
        /// 获取用户某个时间段的餐食记录
        /// </summary>
        public async Task<List<Meal>> GetUserMealsByDateRangeAsync(int userId, DateOnly startDate, DateOnly endDate)
        {
            return await _context.Meals
                .Where(m => m.UserId == userId && m.MealDate >= startDate && m.MealDate <= endDate)
                .Include(m => m.MealFoodImages)
                    .ThenInclude(mfi => mfi.Food)
                .Include(m => m.MealFoodTags)
                    .ThenInclude(mft => mft.Tag)
                .OrderBy(m => m.MealDate)
                .ThenBy(m => m.MealTime)
                .ToListAsync();
        }

        /// <summary>
        /// 获取用户特定类型的餐食记录
        /// </summary>
        public async Task<List<Meal>> GetUserMealsByTypeAsync(int userId, string mealType, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            var query = _context.Meals
                .Where(m => m.UserId == userId && m.MealType == mealType);

            if (startDate.HasValue)
                query = query.Where(m => m.MealDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(m => m.MealDate <= endDate.Value);

            return await query
                .Include(m => m.MealFoodImages)
                    .ThenInclude(mfi => mfi.Food)
                .Include(m => m.MealFoodTags)
                    .ThenInclude(mft => mft.Tag)
                .OrderBy(m => m.MealDate)
                .ThenBy(m => m.MealTime)
                .ToListAsync();
        }

        /// <summary>
        /// 获取餐食详细信息
        /// </summary>
        public async Task<Meal> GetMealDetailsAsync(int mealId)
        {
            return await _context.Meals
                .Include(m => m.User)
                .Include(m => m.MealFoodImages)
                    .ThenInclude(mfi => mfi.Food)
                .Include(m => m.MealFoodTags)
                    .ThenInclude(mft => mft.Food)
                .Include(m => m.MealFoodTags)
                    .ThenInclude(mft => mft.Tag)
                .FirstOrDefaultAsync(m => m.MealId == mealId)
                ?? throw new Exception("餐食记录不存在");
        }
        #endregion

        #region 餐食统计
        /// <summary>
        /// 获取用户餐食统计信息
        /// </summary>
        public async Task<(int TotalMeals, int TodayMeals, Dictionary<string, int> MealTypeCount)> 
            GetUserMealStatsAsync(int userId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            var query = _context.Meals.Where(m => m.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(m => m.MealDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(m => m.MealDate <= endDate.Value);

            var meals = await query.ToListAsync();
            var today = DateOnly.FromDateTime(DateTime.Today);

            var totalMeals = meals.Count;
            var todayMeals = meals.Count(m => m.MealDate == today);
            var mealTypeCount = meals.GroupBy(m => m.MealType)
                .ToDictionary(g => g.Key, g => g.Count());

            return (totalMeals, todayMeals, mealTypeCount);
        }

        /// <summary>
        /// 获取用户最常吃的食物
        /// </summary>
        public async Task<List<(string FoodName, int Count)>> GetUserFavoriteFood(int userId, int topCount = 10)
        {
            return await _context.MealFoodImages
                .Where(mfi => mfi.Meal.UserId == userId)
                .GroupBy(mfi => mfi.Food.Name)
                .Select(g => new { FoodName = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(topCount)
                .Select(x => ValueTuple.Create(x.FoodName, x.Count))
                .ToListAsync();
        }
        #endregion

        #region 餐食管理
        /// <summary>
        /// 更新餐食信息
        /// </summary>
        public async Task<Meal> UpdateMealAsync(int mealId, string? mealType = null, DateOnly? mealDate = null, TimeOnly? mealTime = null)
        {
            var meal = await _context.Meals.FindAsync(mealId)
                ?? throw new Exception("餐食记录不存在");

            if (!string.IsNullOrEmpty(mealType))
                meal.MealType = mealType;

            if (mealDate.HasValue)
                meal.MealDate = mealDate.Value;

            if (mealTime.HasValue)
                meal.MealTime = mealTime.Value;

            await _context.SaveChangesAsync();
            _logger?.LogInformation("更新了餐食记录 {MealId}", mealId);
            
            return meal;
        }

        /// <summary>
        /// 从餐食中移除食物（同时删除OSS图片）
        /// </summary>
        public async Task RemoveFoodFromMealAsync(int mealId, int foodId)
        {
            var mealFoodImage = await _context.MealFoodImages
                .FirstOrDefaultAsync(mfi => mfi.MealId == mealId && mfi.FoodId == foodId);

            if (mealFoodImage != null)
            {
                // 删除OSS中的图片
                if (!string.IsNullOrEmpty(mealFoodImage.ImagePath))
                {
                    await DeleteImageFromOssAsync(mealFoodImage.ImagePath);
                }
                
                _context.MealFoodImages.Remove(mealFoodImage);
            }

            // 同时移除相关的标签
            var mealFoodTags = await _context.MealFoodTags
                .Where(mft => mft.MealId == mealId && mft.FoodId == foodId)
                .ToListAsync();

            _context.MealFoodTags.RemoveRange(mealFoodTags);
            await _context.SaveChangesAsync();
            
            _logger?.LogInformation("从餐食 {MealId} 中移除了食物 {FoodId}", mealId, foodId);
        }

        /// <summary>
        /// 删除餐食记录（同时删除所有相关图片）
        /// </summary>
        public async Task DeleteMealAsync(int mealId)
        {
            var meal = await _context.Meals
                .Include(m => m.MealFoodImages)
                .Include(m => m.MealFoodTags)
                .FirstOrDefaultAsync(m => m.MealId == mealId)
                ?? throw new Exception("餐食记录不存在");

            // 删除OSS中的所有相关图片
            foreach (var mealFoodImage in meal.MealFoodImages)
            {
                if (!string.IsNullOrEmpty(mealFoodImage.ImagePath))
                {
                    await DeleteImageFromOssAsync(mealFoodImage.ImagePath);
                }
            }

            // 删除相关的图片和标签记录
            _context.MealFoodImages.RemoveRange(meal.MealFoodImages);
            _context.MealFoodTags.RemoveRange(meal.MealFoodTags);
            _context.Meals.Remove(meal);

            await _context.SaveChangesAsync();
            _logger?.LogInformation("删除了餐食记录 {MealId}", mealId);
        }
        #endregion

        #region OSS图片管理
        /// <summary>
        /// 上传餐食图片到OSS
        /// </summary>
        public async Task<string> UploadMealImageAsync(int userId, int mealId, int foodId, string localImagePath)
        {
            if (!File.Exists(localImagePath))
                throw new Exception("本地图片文件不存在");

            try
            {
                var fileName = Path.GetFileName(localImagePath);
                var fileExtension = Path.GetExtension(fileName);
                var ossObjectName = $"meals/{userId}/{mealId}/{foodId}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                
                _ossService.UploadFile(ossObjectName, localImagePath);
                
                _logger?.LogInformation("成功上传餐食图片到OSS: {OssPath}", ossObjectName);
                return ossObjectName;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "上传餐食图片失败: {LocalPath}", localImagePath);
                throw new Exception($"图片上传失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从OSS下载餐食图片到本地
        /// </summary>
        public async Task<string> DownloadMealImageAsync(string ossImagePath, string localDownloadPath)
        {
            try
            {
                var fileName = Path.GetFileName(ossImagePath);
                var fullLocalPath = Path.Combine(localDownloadPath, fileName);
                
                // 确保目录存在
                Directory.CreateDirectory(localDownloadPath);
                
                _ossService.DownloadFile(ossImagePath, fullLocalPath);
                
                _logger?.LogInformation("成功从OSS下载图片: {OssPath} -> {LocalPath}", ossImagePath, fullLocalPath);
                return fullLocalPath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "从OSS下载图片失败: {OssPath}", ossImagePath);
                throw new Exception($"图片下载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新餐食中食物的图片（上传新图片并删除旧图片）
        /// </summary>
        public async Task UpdateMealFoodImageAsync(int mealId, int foodId, string newLocalImagePath)
        {
            var mealFoodImage = await _context.MealFoodImages
                .FirstOrDefaultAsync(mfi => mfi.MealId == mealId && mfi.FoodId == foodId)
                ?? throw new Exception("餐食食物记录不存在");

            var meal = await _context.Meals.FindAsync(mealId)
                ?? throw new Exception("餐食记录不存在");

            // 删除旧图片
            if (!string.IsNullOrEmpty(mealFoodImage.ImagePath))
            {
                await DeleteImageFromOssAsync(mealFoodImage.ImagePath);
            }

            // 上传新图片
            var newOssPath = await UploadMealImageAsync(meal.UserId, mealId, foodId, newLocalImagePath);
            
            // 更新数据库记录
            mealFoodImage.ImagePath = newOssPath;
            await _context.SaveChangesAsync();
            
            _logger?.LogInformation("更新了餐食 {MealId} 食物 {FoodId} 的图片", mealId, foodId);
        }

        /// <summary>
        /// 获取餐食中的所有图片OSS路径
        /// </summary>
        public async Task<List<string>> GetMealImagesAsync(int mealId)
        {
            return await _context.MealFoodImages
                .Where(mfi => mfi.MealId == mealId && !string.IsNullOrEmpty(mfi.ImagePath))
                .Select(mfi => mfi.ImagePath)
                .ToListAsync();
        }

        /// <summary>
        /// 批量下载餐食的所有图片
        /// </summary>
        public async Task<List<string>> DownloadAllMealImagesAsync(int mealId, string localDownloadPath)
        {
            var imagePaths = await GetMealImagesAsync(mealId);
            var downloadedPaths = new List<string>();

            foreach (var imagePath in imagePaths)
            {
                try
                {
                    var localPath = await DownloadMealImageAsync(imagePath, localDownloadPath);
                    downloadedPaths.Add(localPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "下载图片失败: {ImagePath}", imagePath);
                }
            }

            return downloadedPaths;
        }

        /// <summary>
        /// 从OSS删除图片的私有方法
        /// </summary>
        private async Task DeleteImageFromOssAsync(string ossImagePath)
        {
            try
            {
                _ossService.DeleteFile(ossImagePath);
                _logger?.LogInformation("删除OSS图片: {OssPath}", ossImagePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除OSS图片失败: {OssPath}", ossImagePath);
                // 不抛出异常，避免影响主要业务流程
            }
        }

        /// <summary>
        /// 获取OSS图片的访问URL（需要根据实际OSS配置实现）
        /// </summary>
        public string GetImageUrl(string ossImagePath)
        {
            if (string.IsNullOrEmpty(ossImagePath))
            {
                Console.WriteLine("GetImageUrl: ossImagePath为空");
                return string.Empty;
            }

            try
            {
                // 根据阿里云OSS的访问规则构建URL
                // 格式: https://{bucketName}.{endpoint}/{objectName}
                var bucketName = OssConfig.BucketName;
                var endpoint = OssConfig.Endpoint.Replace("https://", "");
                var url = $"https://{bucketName}.{endpoint}/{ossImagePath}";
                
                Console.WriteLine($"GetImageUrl: {ossImagePath} -> {url}");
                return url;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetImageUrl failed: {ex.Message}");
                return string.Empty;
            }
        }
        #endregion
    }
}
