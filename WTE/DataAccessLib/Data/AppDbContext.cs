using Microsoft.EntityFrameworkCore;
using DataAccessLib.Models;

namespace DataAccessLib.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Food> Foods { get; set; }
        public DbSet<Meal> Meals { get; set; }
        public DbSet<MealFoodImage> MealFoodImages { get; set; }
        public DbSet<MealFoodTag> MealFoodTags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 配置复合主键
            modelBuilder.Entity<MealFoodImage>()
                .HasKey(mfi => new { mfi.MealId, mfi.FoodId });

            modelBuilder.Entity<MealFoodTag>()
                .HasKey(mft => new { mft.MealId, mft.FoodId, mft.TagId });

            // 配置枚举转换
            modelBuilder.Entity<Meal>()
                .Property(m => m.MealType)
                .HasConversion<string>();

            // 配置关系
            modelBuilder.Entity<Meal>()
                .HasMany(m => m.MealFoodImages)
                .WithOne(mfi => mfi.Meal)
                .HasForeignKey(mfi => mfi.MealId);

            modelBuilder.Entity<Meal>()
                .HasMany(m => m.MealFoodTags)
                .WithOne(mft => mft.Meal)
                .HasForeignKey(mft => mft.MealId);

            modelBuilder.Entity<Food>()
                .HasMany(f => f.MealFoodImages)
                .WithOne(mfi => mfi.Food)
                .HasForeignKey(mfi => mfi.FoodId);

            modelBuilder.Entity<Food>()
                .HasMany(f => f.MealFoodTags)
                .WithOne(mft => mft.Food)
                .HasForeignKey(mft => mft.FoodId);

            modelBuilder.Entity<Tag>()
                .HasMany(t => t.MealFoodTags)
                .WithOne(mft => mft.Tag)
                .HasForeignKey(mft => mft.TagId);
        }
    }
}