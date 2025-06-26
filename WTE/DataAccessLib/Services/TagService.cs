using DataAccessLib.Data;
using DataAccessLib.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataAccessLib.Services
{
    public class TagService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TagService> _logger;

        public TagService(AppDbContext context, ILogger<TagService> logger = null)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 获取或创建标签
        /// </summary>
        public async Task<int> GetOrCreateTagAsync(string tagName)
        {
            try
            {
                // 查找是否已存在该标签
                var existingTag = await _context.Tags
                    .FirstOrDefaultAsync(t => t.TagName == tagName);

                if (existingTag != null)
                {
                    _logger?.LogInformation("找到已存在的标签记录: {TagName}, ID: {TagId}", tagName, existingTag.TagId);
                    return existingTag.TagId;
                }

                // 创建新标签记录
                var newTag = new Tag
                {
                    TagName = tagName
                };

                _context.Tags.Add(newTag);
                await _context.SaveChangesAsync();

                _logger?.LogInformation("创建新标签记录成功: {TagName}, ID: {TagId}", tagName, newTag.TagId);
                return newTag.TagId;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取或创建标签记录失败: {TagName}", tagName);
                throw new Exception($"处理标签记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有标签
        /// </summary>
        public async Task<List<Tag>> GetAllTagsAsync()
        {
            try
            {
                return await _context.Tags.OrderBy(t => t.TagName).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取所有标签失败");
                throw new Exception($"获取标签列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据ID获取标签
        /// </summary>
        public async Task<Tag> GetTagByIdAsync(int tagId)
        {
            try
            {
                return await _context.Tags.FindAsync(tagId)
                    ?? throw new Exception("标签不存在");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取标签失败: TagId={TagId}", tagId);
                throw;
            }
        }

        /// <summary>
        /// 根据名称搜索标签
        /// </summary>
        public async Task<List<Tag>> SearchTagsByNameAsync(string searchTerm)
        {
            try
            {
                return await _context.Tags
                    .Where(t => t.TagName.Contains(searchTerm))
                    .OrderBy(t => t.TagName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "搜索标签失败: SearchTerm={SearchTerm}", searchTerm);
                throw new Exception($"搜索标签失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除标签
        /// </summary>
        public async Task DeleteTagAsync(int tagId)
        {
            try
            {
                var tag = await _context.Tags.FindAsync(tagId)
                    ?? throw new Exception("标签不存在");

                _context.Tags.Remove(tag);
                await _context.SaveChangesAsync();

                _logger?.LogInformation("删除标签成功: TagId={TagId}", tagId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除标签失败: TagId={TagId}", tagId);
                throw;
            }
        }

        /// <summary>
        /// 获取用户历史使用过的标签
        /// </summary>
        public async Task<List<Tag>> GetUserHistoryTagsAsync(int userId, int limit = 30)
        {
            try
            {
                _logger?.LogInformation("开始获取用户历史标签: UserId={UserId}, Limit={Limit}", userId, limit);
                
                var result = await _context.MealFoodTags
                    .Where(mft => mft.Meal.UserId == userId)
                    .GroupBy(mft => mft.Tag)
                    .OrderByDescending(g => g.Count()) // 按使用频率排序
                    .Take(limit)
                    .Select(g => g.Key)
                    .ToListAsync();
                
                _logger?.LogInformation("获取用户历史标签成功: UserId={UserId}, Count={Count}", userId, result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取用户历史标签失败: UserId={UserId}", userId);
                throw new Exception($"获取历史标签失败: {ex.Message}");
            }
        }

        public List<(int TagId, string TagName, int Count)> GetUserTagStatistics(int userId, DateTime fromDateTime, DateTime toDateTime)
        {
            try
            {
                Console.WriteLine($"查询标签统计 - 用户ID: {userId}, 开始日期: {fromDateTime:yyyy-MM-dd}, 结束日期: {toDateTime:yyyy-MM-dd}");
                
                var fromDate = DateOnly.FromDateTime(fromDateTime);
                var toDate = DateOnly.FromDateTime(toDateTime);

                var result = _context.MealFoodTags
                    .Include(m => m.Meal)
                    .Include(m => m.Tag)
                    .Where(m => m.Meal.UserId == userId &&
                           m.Meal.MealDate >= fromDate &&
                           m.Meal.MealDate <= toDate)
                    .GroupBy(m => new { m.TagId, m.Tag.TagName })
                    .Select(g => new
                    {
                        TagId = g.Key.TagId,
                        TagName = g.Key.TagName,
                        Count = g.Count()
                    })
                    .ToList();

                Console.WriteLine($"查询到 {result.Count} 个标签统计结果");
                foreach (var item in result)
                {
                    Console.WriteLine($"标签: {item.TagName}, ID: {item.TagId}, 数量: {item.Count}");
                }

                return result.Select(r => (r.TagId, r.TagName, r.Count)).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取标签统计时出错: {ex}");
                throw;
            }
        }
    }
}
