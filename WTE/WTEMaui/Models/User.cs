using SQLite;

namespace WTEMaui.Models
{
    [Table("Users")]
    public class User
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [Unique, NotNull]
        public string Username { get; set; } = string.Empty;
        
        [NotNull]
        public string Password { get; set; } = string.Empty;
        
        [NotNull]
        public string Email { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime LastLoginAt { get; set; }
    }
} 