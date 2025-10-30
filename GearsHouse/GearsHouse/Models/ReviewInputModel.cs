using System.ComponentModel.DataAnnotations;

namespace GearsHouse.Models
{
    public class ReviewInputModel
    {
        [Required]
        public int ProductId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        public string Comment { get; set; }
    }
}
