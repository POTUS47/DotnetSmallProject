using DataAccessLib.Data;
using DataAccessLib.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    }
}
