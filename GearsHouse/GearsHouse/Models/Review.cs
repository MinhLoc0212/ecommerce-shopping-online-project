using System.ComponentModel.DataAnnotations;

namespace GearsHouse.Models
{
    public class Review
    {
        public int Id { get; set; }
       
        public string UserId { get; set; }

        public string UserName { get; set; }
       
        public int ProductId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        public string Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
