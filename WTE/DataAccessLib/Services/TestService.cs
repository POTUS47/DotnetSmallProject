using DataAccessLib.Data;
using DataAccessLib.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLib.Services
{
    public class TestService
    {
        private readonly AppDbContext _context;

        public TestService(AppDbContext context)
        {
            _context = context;
        }

        // 测试数据库连接是否正常
        public async Task<bool> CanConnectAsync()
        {
            try
            {
                return await _context.Database.CanConnectAsync();
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 添加测试用户
        public async Task<User> AddTestUserAsync()
        {
            var testUser = new User
            {
                Username = "testuser_" + Guid.NewGuid().ToString()[..8],
                Email = $"test{Guid.NewGuid().ToString()[..4]}@example.com",
                Password = "testpassword",
                Height = 170.5m,
                Weight = 65.2m
            };

            _context.Users.Add(testUser);
            await _context.SaveChangesAsync();
            return testUser;
        }

        // 获取所有用户
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }
    }
}