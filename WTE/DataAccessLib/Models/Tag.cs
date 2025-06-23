using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccessLib.Models
{
    [Table("Tag")]
    public class Tag
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TagId { get; set; }

        [Required]
        [MaxLength(100)]
        public string TagName { get; set; }

        public ICollection<MealFoodTag> MealFoodTags { get; set; } = new List<MealFoodTag>();
    }
}