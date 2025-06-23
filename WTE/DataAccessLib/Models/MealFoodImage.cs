using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccessLib.Models
{
    [Table("Meal_Food_Image")]
    public class MealFoodImage
    {
        [Key]
        [Column(Order = 0)]
        public int MealId { get; set; }

        [Key]
        [Column(Order = 1)]
        public int FoodId { get; set; }

        [MaxLength(200)]
        public string? ImagePath { get; set; }

        [ForeignKey("MealId")]
        public Meal Meal { get; set; }

        [ForeignKey("FoodId")]
        public Food Food { get; set; }
    }
}