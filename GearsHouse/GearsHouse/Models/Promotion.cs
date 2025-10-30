using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace GearsHouse.Models
{
    public class Promotion
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Range(0, 100)]
        [Display(Name = "Phần trăm giảm giá (%)")]
        public int DiscountPercent { get; set; }

        [Display(Name = "Ngày bắt đầu")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Display(Name = "Ngày kết thúc")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [NotMapped]
        public bool IsActive => DateTime.Now >= StartDate && DateTime.Now <= EndDate;
    }
}
