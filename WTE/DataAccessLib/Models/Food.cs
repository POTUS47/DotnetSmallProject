using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccessLib.Models
{
    [Table("Food")]
    public class Food
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FoodId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        public ICollection<MealFoodImage> MealFoodImages { get; set; } = new List<MealFoodImage>();
        public ICollection<MealFoodTag> MealFoodTags { get; set; } = new List<MealFoodTag>();
    }
}