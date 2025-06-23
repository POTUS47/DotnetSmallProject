using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccessLib.Models
{
    [Table("Meals")]
    public class Meal
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MealId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(10)]
        public string MealType { get; set; } // "早饭","午饭","晚饭","零食"

        [Required]
        public DateOnly MealDate { get; set; }

        [Required]
        public TimeOnly MealTime { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        public ICollection<MealFoodImage> MealFoodImages { get; set; } = new List<MealFoodImage>();
        public ICollection<MealFoodTag> MealFoodTags { get; set; } = new List<MealFoodTag>();
    }
}