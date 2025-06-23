using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccessLib.Models
{
    [Table("Meal_Food_Tag")]
    public class MealFoodTag
    {
        [Key]
        [Column(Order = 0)]
        public int MealId { get; set; }

        [Key]
        [Column(Order = 1)]
        public int FoodId { get; set; }

        [Key]
        [Column(Order = 2)]
        public int TagId { get; set; }

        [ForeignKey("MealId")]
        public Meal Meal { get; set; }

        [ForeignKey("FoodId")]
        public Food Food { get; set; }

        [ForeignKey("TagId")]
        public Tag Tag { get; set; }
    }
}