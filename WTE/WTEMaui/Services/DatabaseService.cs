using SQLite;
using WTEMaui.Models;

namespace WTEMaui.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _database;

        public DatabaseService()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "wte_users.db");
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<User>().Wait();
        }

        // 注册新用户
        public async Task<bool> RegisterUserAsync(string username, string password, string email)
        {
            try
            {
                // 检查用户名是否已存在
                var existingUser = await _database.Table<User>()
                    .Where(u => u.Username == username)
                    .FirstOrDefaultAsync();

                if (existingUser != null)
                {
                    return false; // 用户名已存在
                }

                // 检查邮箱是否已存在
                var existingEmail = await _database.Table<User>()
                    .Where(u => u.Email == email)
                    .FirstOrDefaultAsync();

                if (existingEmail != null)
                {
                    return false; // 邮箱已存在
                }

                // 创建新用户
                var user = new User
                {
                    Username = username,
                    Password = password, // 实际应用中应该加密
                    Email = email,
                    CreatedAt = DateTime.Now
                };

                await _database.InsertAsync(user);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 用户登录
        public async Task<User> LoginAsync(string username, string password)
        {
            try
            {
                var user = await _database.Table<User>()
                    .Where(u => u.Username == username && u.Password == password)
                    .FirstOrDefaultAsync();

                if (user != null)
                {
                    // 更新最后登录时间
                    user.LastLoginAt = DateTime.Now;
                    await _database.UpdateAsync(user);
                }

                return user;
            }
            catch
            {
                return null;
            }
        }

        // 根据用户名获取用户
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _database.Table<User>()
                .Where(u => u.Username == username)
                .FirstOrDefaultAsync();
        }

        // 获取所有用户（用于调试）
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _database.Table<User>().ToListAsync();
        }

        // 删除用户
        public async Task<bool> DeleteUserAsync(int userId)
        {
            try
            {
                await _database.DeleteAsync<User>(userId);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
} 