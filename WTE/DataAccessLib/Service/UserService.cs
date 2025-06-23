using DataAccessLib.Data;
using DataAccessLib.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace DataAccessLib.Services
{
    public class UserService
    {
        private readonly AppDbContext _context;

        public UserService(AppDbContext context)
        {
            _context = context;
        }

        // 密码加密方法
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        #region 用户注册与登录
        /// <summary>
        /// 注册新用户
        /// </summary>
        public async Task<User> RegisterAsync(string username, string email, string password)
        {
            if (await _context.Users.AnyAsync(u => u.Username == username))
                throw new Exception("用户名已存在");

            if (await _context.Users.AnyAsync(u => u.Email == email))
                throw new Exception("邮箱已被注册");

            var user = new User
            {
                Username = username,
                Email = email,
                Password = HashPassword(password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        public async Task<User> LoginAsync(string usernameOrEmail, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == usernameOrEmail || u.Email == usernameOrEmail);

            if (user == null || user.Password != HashPassword(password))
                throw new Exception("用户名或密码错误");

            return user;
        }
        #endregion

        #region 用户信息管理
        /// <summary>
        /// 获取用户基本信息
        /// </summary>
        public async Task<User> GetUserInfoAsync(int userId)
        {
            return await _context.Users.FindAsync(userId)
                ?? throw new Exception("用户不存在");
        }

        /// <summary>
        /// 更新用户基本信息
        /// </summary>
        public async Task<User> UpdateUserInfoAsync(int userId, Action<User> updateAction)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new Exception("用户不存在");

            updateAction(user);

            await _context.SaveChangesAsync();
            return user;
        }

        /// <summary>
        /// 更新密码
        /// </summary>
        public async Task ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new Exception("用户不存在");

            if (user.Password != HashPassword(oldPassword))
                throw new Exception("旧密码不正确");

            user.Password = HashPassword(newPassword);
            await _context.SaveChangesAsync();
        }
        #endregion

        #region 健康数据管理
        /// <summary>
        /// 更新用户健康数据
        /// </summary>
        public async Task UpdateHealthDataAsync(int userId, decimal? height, decimal? weight, string? healthGoal, string? allergies)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new Exception("用户不存在");

            user.Height = height;
            user.Weight = weight;
            user.HealthGoal = healthGoal;
            user.Allergies = allergies;

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 获取用户健康数据
        /// </summary>
        public async Task<(decimal? Height, decimal? Weight, string? HealthGoal, string? Allergies)>
            GetHealthDataAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new Exception("用户不存在");

            return (user.Height, user.Weight, user.HealthGoal, user.Allergies);
        }
        #endregion

        #region 管理员功能
        /// <summary>
        /// 获取所有用户（仅管理员）
        /// </summary>
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        /// <summary>
        /// 删除用户（仅管理员）
        /// </summary>
        public async Task DeleteUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new Exception("用户不存在");

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
        #endregion
    }
}